using Sentier.Data.Records;
using Sentier.Definitions;
using Sentier.UserTasks;

namespace Sentier.Execution;

internal sealed partial class ProcessEngine
{
    private Task<NodeOutcome> ExecuteNodeAsync(InstanceRun run, NodeDefinition node, NodeInstanceRecord nodeInstance, CancellationToken ct)
        => node switch
        {
            StartNode => Task.FromResult(NodeOutcome.Continue()),
            SystemNode system => ExecuteSystemAsync(run, system, nodeInstance, ct),
            TimerWaitNode timer => ExecuteTimerAsync(run, timer, nodeInstance, ct),
            SignalWaitNode signal => ExecuteSignalAsync(run, signal, nodeInstance, ct),
            UserTaskNode userTask => ExecuteUserTaskAsync(run, userTask, nodeInstance, ct),
            DecisionNode decision => ExecuteDecisionAsync(run, decision, nodeInstance, ct),
            SubProcessNode subProcess => ExecuteSubProcessAsync(run, subProcess, nodeInstance, ct),
            EndNode => Task.FromResult(NodeOutcome.End),
            _ => throw new SentierExecutionException($"Type de noeud non supporté : {node.GetType().Name}."),
        };

    private NodeExecutionContext CreateContext(InstanceRun run, NodeDefinition node, NodeInstanceRecord nodeInstance)
        => new(run, node, nodeInstance.Id, _session.Connection, _session.Transaction, _clock.UtcNow);

    private async Task<NodeOutcome> ExecuteSystemAsync(InstanceRun run, SystemNode node, NodeInstanceRecord nodeInstance, CancellationToken ct)
    {
        var handler = _handlers.GetSystemTask(node.Handler);
        var context = CreateContext(run, node, nodeInstance);
        await handler.ExecuteAsync(context, ct);
        return NodeOutcome.Continue(context.SelectedTransition);
    }

    private async Task<NodeOutcome> ExecuteTimerAsync(InstanceRun run, TimerWaitNode node, NodeInstanceRecord nodeInstance, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var dueAt = ComputeDueDate(run, node, now);
        if (dueAt <= now)
        {
            return NodeOutcome.Continue();
        }

        await _timers.InsertAsync(new TimerRecord
        {
            ProcessInstanceId = run.Record.Id,
            NodeInstanceId = nodeInstance.Id,
            DueAt = dueAt,
            Status = WaitStatus.Pending,
            CreatedAt = now,
        }, ct);

        return NodeOutcome.Wait;
    }

    private static DateTime ComputeDueDate(InstanceRun run, TimerWaitNode node, DateTime now)
    {
        if (!string.IsNullOrEmpty(node.DueDateVariable))
        {
            var due = run.Variables.Get<DateTime?>(node.DueDateVariable)
                      ?? throw new SentierExecutionException($"Attente '{node.Id}' : la variable d'échéance '{node.DueDateVariable}' est vide.");
            return due.Kind switch
            {
                DateTimeKind.Utc => due,
                DateTimeKind.Local => due.ToUniversalTime(),
                _ => DateTime.SpecifyKind(due, DateTimeKind.Utc),
            };
        }

        if (!string.IsNullOrEmpty(node.DurationVariable))
        {
            var duration = run.Variables.Get<TimeSpan?>(node.DurationVariable)
                           ?? throw new SentierExecutionException($"Attente '{node.Id}' : la variable de durée '{node.DurationVariable}' est vide.");
            return now.Add(duration);
        }

        return now.Add(node.Duration ?? throw new SentierExecutionException($"Attente '{node.Id}' : aucune durée définie."));
    }

    private async Task<NodeOutcome> ExecuteSignalAsync(InstanceRun run, SignalWaitNode node, NodeInstanceRecord nodeInstance, CancellationToken ct)
    {
        var correlationKey = !string.IsNullOrEmpty(node.CorrelationVariable)
            ? run.Variables.GetString(node.CorrelationVariable)
            : node.CorrelateOnBusinessKey ? run.Record.BusinessKey : null;

        await _signals.InsertAsync(new SignalSubscriptionRecord
        {
            ProcessInstanceId = run.Record.Id,
            NodeInstanceId = nodeInstance.Id,
            SignalName = node.SignalName,
            CorrelationKey = correlationKey,
            Status = WaitStatus.Pending,
            CreatedAt = _clock.UtcNow,
        }, ct);

        return NodeOutcome.Wait;
    }

    private async Task<NodeOutcome> ExecuteUserTaskAsync(InstanceRun run, UserTaskNode node, NodeInstanceRecord nodeInstance, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var task = new UserTaskRecord
        {
            ProcessInstanceId = run.Record.Id,
            NodeInstanceId = nodeInstance.Id,
            TaskCode = string.IsNullOrEmpty(node.TaskCode) ? node.Id : node.TaskCode,
            Status = WaitStatus.Pending,
            CreatedAt = now,
        };
        await _userTasks.InsertAsync(task, ct);

        var assignee = !string.IsNullOrEmpty(node.AssigneeVariable)
            ? run.Variables.GetString(node.AssigneeVariable) ?? node.Assignee
            : node.Assignee;

        var externalId = await _taskPool.CreateTaskAsync(new UserTaskRequest
        {
            UserTaskId = task.Id,
            ProcessInstanceId = run.Record.Id,
            RootProcessInstanceId = run.Record.RootInstanceId,
            NodeInstanceId = nodeInstance.Id,
            DefinitionKey = run.Definition.Key,
            DefinitionVersion = run.Definition.Version,
            BusinessKey = run.Record.BusinessKey,
            NodeId = node.Id,
            TaskCode = task.TaskCode,
            Title = node.Title ?? node.Name,
            CandidateGroups = node.CandidateGroups.ToArray(),
            Assignee = assignee,
            CreatedAt = now,
            DueAt = node.DueIn is { } dueIn ? now.Add(dueIn) : null,
            Properties = new Dictionary<string, string>(node.Properties, StringComparer.Ordinal),
            Variables = run.Variables.ToReadOnly(),
        }, ct);

        if (!string.IsNullOrEmpty(externalId))
        {
            await _userTasks.SetExternalIdAsync(task.Id, externalId, ct);
        }

        return NodeOutcome.Wait;
    }

    private async Task<NodeOutcome> ExecuteDecisionAsync(InstanceRun run, DecisionNode node, NodeInstanceRecord nodeInstance, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(node.Handler))
        {
            var context = CreateContext(run, node, nodeInstance);
            var name = await _handlers.GetDecision(node.Handler).DecideAsync(context, ct);
            return NodeOutcome.Continue(name, strict: true);
        }

        // Décision par variable : une valeur sans transition correspondante prend la transition par défaut.
        var value = string.IsNullOrEmpty(node.Variable) ? null : run.Variables.GetString(node.Variable);
        return NodeOutcome.Continue(value, strict: false);
    }

    private async Task<NodeOutcome> ExecuteSubProcessAsync(InstanceRun run, SubProcessNode node, NodeInstanceRecord nodeInstance, CancellationToken ct)
    {
        if (run.Depth + 1 > _options.MaxSubProcessDepth)
        {
            throw new SentierExecutionException(
                $"Sous-processus '{node.DefinitionKey}' : profondeur maximale ({_options.MaxSubProcessDepth}) atteinte (appel récursif ?).");
        }

        var definition = await _definitions.GetLatestAsync(node.DefinitionKey, ct)
                         ?? throw new SentierNotFoundException($"Sous-processus '{node.DefinitionKey}' : aucune définition déployée.");

        var childVariables = new ProcessVariables();
        if (node.Inputs is null)
        {
            childVariables.Merge(run.Variables);
        }
        else
        {
            foreach (var (childName, parentName) in node.Inputs)
            {
                if (run.Variables.Contains(parentName))
                {
                    childVariables[childName] = run.Variables[parentName];
                }
            }
        }

        var child = await CreateInstanceAsync(definition, run.Record.BusinessKey, childVariables, run, nodeInstance.Id, ct);
        await RunFromAsync(child, definition.GetStartNode(), ct);

        if (child.Record.Status == ProcessInstanceStatus.Completed)
        {
            // Terminé pendant son démarrage : on récupère les sorties et on continue sans suspendre le parent.
            ApplyOutputs(child.Variables, run.Variables, node.Outputs);
            return NodeOutcome.Continue();
        }

        return NodeOutcome.Wait;
    }

    private static void ApplyOutputs(ProcessVariables child, ProcessVariables parent, IReadOnlyDictionary<string, string>? outputs)
    {
        if (outputs is null)
        {
            parent.Merge(child);
            return;
        }

        foreach (var (parentName, childName) in outputs)
        {
            if (child.Contains(childName))
            {
                parent[parentName] = child[childName];
            }
        }
    }
}
