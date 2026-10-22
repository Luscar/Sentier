using Sentier.Data.Records;
using Sentier.Definitions;
using Sentier.UserTasks;

namespace Sentier.Execution;

internal sealed partial class ProcessEngine
{
    /// <summary>
    /// Boucle d'exécution : enchaîne les noeuds à partir de <paramref name="node"/> jusqu'à une attente ou une fin.
    /// L'instance n'est écrite qu'à ces points d'arrêt ; tout se fait dans la transaction du client.
    /// </summary>
    private async Task RunFromAsync(InstanceRun run, NodeDefinition node, CancellationToken ct)
    {
        var current = node;
        var steps = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (++steps > _options.MaxStepsPerRun)
            {
                throw new SentierExecutionException(
                    $"Instance {run.Record.Id} : plus de {_options.MaxStepsPerRun} noeuds exécutés sans point d'arrêt (boucle infinie ?).");
            }

            var nodeInstance = new NodeInstanceRecord
            {
                ProcessInstanceId = run.Record.Id,
                NodeId = current.Id,
                NodeType = current.Type.ToString(),
                Status = NodeInstanceStatus.Active,
                StartedAt = _clock.UtcNow,
            };
            await _nodeInstances.InsertAsync(nodeInstance, ct);
            run.Record.CurrentNodeId = current.Id;

            NodeOutcome outcome;
            try
            {
                outcome = await ExecuteNodeAsync(run, current, nodeInstance, ct);
            }
            catch (SentierException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new SentierExecutionException(
                    $"Échec du noeud '{current.Id}' ({current.Type}) de l'instance {run.Record.Id} du processus '{run.Definition.Key}'.", ex);
            }

            switch (outcome.Kind)
            {
                case NodeOutcomeKind.Continue:
                    var transition = run.Definition.ResolveOutgoing(current, outcome.TransitionName, outcome.Strict);
                    await _nodeInstances.CompleteAsync(nodeInstance.Id, transition.Id, _clock.UtcNow, ct);
                    current = run.Definition.GetNode(transition.To);
                    break;

                case NodeOutcomeKind.Wait:
                    await _nodeInstances.SetWaitingAsync(nodeInstance.Id, ct);
                    run.Record.Status = ProcessInstanceStatus.Waiting;
                    await SaveAsync(run, ct);
                    return;

                case NodeOutcomeKind.End:
                    await _nodeInstances.CompleteAsync(nodeInstance.Id, null, _clock.UtcNow, ct);
                    await CompleteInstanceAsync(run, ct);
                    return;

                default:
                    throw new SentierExecutionException($"Résultat de noeud inattendu : {outcome.Kind}.");
            }
        }
    }

    /// <summary>Reprend une instance suspendue sur le noeud <paramref name="nodeInstanceId"/>.</summary>
    private async Task ResumeWaitingAsync(long processInstanceId, long nodeInstanceId, string? transitionName, bool strict, IEnumerable<KeyValuePair<string, object?>>? variables, CancellationToken ct)
    {
        var run = await LoadRunAsync(processInstanceId, forUpdate: true, ct);
        var nodeInstance = await GetNodeInstanceAsync(nodeInstanceId, ct);
        run.Variables.Merge(variables);
        await ContinueAfterWaitAsync(run, nodeInstance, transitionName, strict, ct);
    }

    private async Task ContinueAfterWaitAsync(InstanceRun run, NodeInstanceRecord nodeInstance, string? transitionName, bool strict, CancellationToken ct)
    {
        if (run.Record.Status != ProcessInstanceStatus.Waiting)
        {
            throw new SentierStateException($"L'instance {run.Record.Id} n'est pas en attente (statut {run.Record.Status}).");
        }

        if (nodeInstance.ProcessInstanceId != run.Record.Id || nodeInstance.Status != NodeInstanceStatus.Waiting)
        {
            throw new SentierStateException($"Le noeud {nodeInstance.Id} n'est pas en attente dans l'instance {run.Record.Id}.");
        }

        var node = run.Definition.GetNode(nodeInstance.NodeId);
        var transition = run.Definition.ResolveOutgoing(node, transitionName, strict);
        await _nodeInstances.CompleteAsync(nodeInstance.Id, transition.Id, _clock.UtcNow, ct);

        run.Record.Status = ProcessInstanceStatus.Running;
        await RunFromAsync(run, run.Definition.GetNode(transition.To), ct);
    }

    private async Task CompleteInstanceAsync(InstanceRun run, CancellationToken ct)
    {
        run.Record.Status = ProcessInstanceStatus.Completed;
        run.Record.EndedAt = _clock.UtcNow;
        await SaveAsync(run, ct);

        if (run.Record.ParentNodeInstanceId is { } parentNodeInstanceId && run.Record.ParentInstanceId is { } parentId)
        {
            await ResumeParentAsync(run, parentId, parentNodeInstanceId, ct);
        }
    }

    /// <summary>
    /// Fin d'un sous-processus. Si le parent est déjà suspendu sur le noeud sous-processus (fin asynchrone), il est repris.
    /// Si le noeud parent est encore actif, le sous-processus s'est terminé pendant son démarrage :
    /// le noeud sous-processus du parent récupère lui-même les variables et continue.
    /// </summary>
    private async Task ResumeParentAsync(InstanceRun child, long parentId, long parentNodeInstanceId, CancellationToken ct)
    {
        var parentNodeInstance = await GetNodeInstanceAsync(parentNodeInstanceId, ct);
        if (parentNodeInstance.Status != NodeInstanceStatus.Waiting)
        {
            return;
        }

        var parent = await LoadRunAsync(parentId, forUpdate: true, ct);
        if (parent.Definition.GetNode(parentNodeInstance.NodeId) is SubProcessNode subProcess)
        {
            ApplyOutputs(child.Variables, parent.Variables, subProcess.Outputs);
        }

        await ContinueAfterWaitAsync(parent, parentNodeInstance, null, true, ct);
    }

    private async Task CancelRunAsync(InstanceRun run, string? reason, CancellationToken ct)
    {
        var id = run.Record.Id;
        var now = _clock.UtcNow;

        foreach (var childId in await _instances.GetOpenChildIdsAsync(id, ct))
        {
            var child = await LoadRunAsync(childId, forUpdate: true, ct);
            await CancelRunAsync(child, reason, ct);
        }

        await _timers.CancelPendingAsync(id, ct);
        await _signals.CancelPendingAsync(id, ct);

        foreach (var task in await _userTasks.GetPendingForInstanceAsync(id, ct))
        {
            await _userTasks.CancelAsync(task.Id, now, ct);
            if (!string.IsNullOrEmpty(task.ExternalId))
            {
                await _taskPool.CancelTaskAsync(new UserTaskCancellation
                {
                    UserTaskId = task.Id,
                    ExternalId = task.ExternalId,
                    ProcessInstanceId = id,
                    Reason = reason,
                }, ct);
            }
        }

        await _nodeInstances.CancelOpenAsync(id, now, ct);

        run.Record.Status = ProcessInstanceStatus.Cancelled;
        run.Record.EndedAt = now;
        await SaveAsync(run, ct);
    }

    private async Task<InstanceRun> CreateInstanceAsync(
        ProcessDefinition definition,
        string? businessKey,
        ProcessVariables variables,
        InstanceRun? parent,
        long? parentNodeInstanceId,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var id = await _instances.NextIdAsync(ct);

        var record = new ProcessInstanceRecord
        {
            Id = id,
            DefinitionId = definition.Id,
            DefinitionKey = definition.Key,
            DefinitionVersion = definition.Version,
            BusinessKey = businessKey,
            Status = ProcessInstanceStatus.Running,
            Variables = variables.ToJson(),
            ParentInstanceId = parent?.Record.Id,
            ParentNodeInstanceId = parentNodeInstanceId,
            RootInstanceId = parent?.Record.RootInstanceId ?? id,
            RowVersion = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _instances.InsertAsync(record, ct);

        return new InstanceRun(record, definition, variables, parent is null ? 0 : parent.Depth + 1);
    }

    private async Task<InstanceRun> LoadRunAsync(long processInstanceId, bool forUpdate, CancellationToken ct)
    {
        var record = await _instances.GetAsync(processInstanceId, forUpdate, ct)
                     ?? throw new SentierNotFoundException($"Instance de processus {processInstanceId} introuvable.");
        var definition = await _definitions.GetAsync(record.DefinitionId, ct)
                         ?? throw new SentierNotFoundException($"Définition {record.DefinitionId} de l'instance {processInstanceId} introuvable.");
        return new InstanceRun(record, definition, ProcessVariables.FromJson(record.Variables), 0);
    }

    private async Task<NodeInstanceRecord> GetNodeInstanceAsync(long nodeInstanceId, CancellationToken ct)
        => await _nodeInstances.GetAsync(nodeInstanceId, ct)
           ?? throw new SentierNotFoundException($"Instance de noeud {nodeInstanceId} introuvable.");

    private Task SaveAsync(InstanceRun run, CancellationToken ct)
    {
        run.Record.Variables = run.Variables.ToJson();
        run.Record.UpdatedAt = _clock.UtcNow;
        return _instances.UpdateAsync(run.Record, ct);
    }
}
