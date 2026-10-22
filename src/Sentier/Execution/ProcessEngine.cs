using Microsoft.Extensions.Options;
using Sentier.Configuration;
using Sentier.Data;
using Sentier.Data.Records;
using Sentier.Data.Stores;
using Sentier.Definitions;
using Sentier.Handlers;
using Sentier.UserTasks;

namespace Sentier.Execution;

/// <summary>API publique du moteur. La boucle d'exécution est dans ProcessEngine.Run.cs, les noeuds dans ProcessEngine.Nodes.cs.</summary>
internal sealed partial class ProcessEngine : IProcessEngine
{
    private readonly ISentierDbSession _session;
    private readonly IProcessDefinitionService _definitions;
    private readonly ProcessInstanceStore _instances;
    private readonly NodeInstanceStore _nodeInstances;
    private readonly TimerStore _timers;
    private readonly SignalSubscriptionStore _signals;
    private readonly UserTaskStore _userTasks;
    private readonly IHandlerResolver _handlers;
    private readonly ITaskPool _taskPool;
    private readonly ISentierClock _clock;
    private readonly SentierOptions _options;

    public ProcessEngine(
        ISentierDbSession session,
        IProcessDefinitionService definitions,
        ProcessInstanceStore instances,
        NodeInstanceStore nodeInstances,
        TimerStore timers,
        SignalSubscriptionStore signals,
        UserTaskStore userTasks,
        IHandlerResolver handlers,
        ITaskPool taskPool,
        ISentierClock clock,
        IOptions<SentierOptions> options)
    {
        _session = session;
        _definitions = definitions;
        _instances = instances;
        _nodeInstances = nodeInstances;
        _timers = timers;
        _signals = signals;
        _userTasks = userTasks;
        _handlers = handlers;
        _taskPool = taskPool;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<long> StartProcessAsync(string definitionKey, string? businessKey = null, IDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var definition = await _definitions.GetLatestAsync(definitionKey, cancellationToken)
                         ?? throw new SentierNotFoundException($"Aucune définition déployée pour le processus '{definitionKey}'.");
        return await StartAsync(definition, businessKey, variables, cancellationToken);
    }

    public async Task<long> StartProcessByIdAsync(long definitionId, string? businessKey = null, IDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var definition = await _definitions.GetAsync(definitionId, cancellationToken)
                         ?? throw new SentierNotFoundException($"Définition de processus {definitionId} introuvable.");
        return await StartAsync(definition, businessKey, variables, cancellationToken);
    }

    public async Task CompleteUserTaskAsync(long userTaskId, UserTaskCompletion? completion = null, CancellationToken cancellationToken = default)
    {
        var task = await _userTasks.GetAsync(userTaskId, cancellationToken)
                   ?? throw new SentierNotFoundException($"Tâche utilisateur {userTaskId} introuvable.");
        await CompleteUserTaskCoreAsync(task, completion, cancellationToken);
    }

    public async Task CompleteUserTaskByExternalIdAsync(string externalTaskId, UserTaskCompletion? completion = null, CancellationToken cancellationToken = default)
    {
        var task = await _userTasks.GetByExternalIdAsync(externalTaskId, cancellationToken)
                   ?? throw new SentierNotFoundException($"Aucune tâche utilisateur liée à la tâche externe '{externalTaskId}'.");
        await CompleteUserTaskCoreAsync(task, completion, cancellationToken);
    }

    public async Task<int> SendSignalAsync(string signalName, string? correlationKey = null, IDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var subscriptions = await _signals.FindPendingAsync(signalName, correlationKey, cancellationToken);
        var resumed = 0;

        foreach (var subscription in subscriptions)
        {
            if (!await _signals.MarkReceivedAsync(subscription.Id, _clock.UtcNow, cancellationToken))
            {
                continue;
            }

            await ResumeWaitingAsync(subscription.ProcessInstanceId, subscription.NodeInstanceId, null, true, variables, cancellationToken);
            resumed++;
        }

        return resumed;
    }

    public Task<IReadOnlyList<long>> GetDueTimersAsync(int maxCount = 100, CancellationToken cancellationToken = default)
        => _timers.GetDueIdsAsync(_clock.UtcNow, maxCount, cancellationToken);

    public async Task<bool> FireTimerAsync(long timerId, CancellationToken cancellationToken = default)
    {
        var timer = await _timers.GetAsync(timerId, cancellationToken)
                    ?? throw new SentierNotFoundException($"Minuterie {timerId} introuvable.");

        if (timer.Status != WaitStatus.Pending || !await _timers.MarkFiredAsync(timer.Id, _clock.UtcNow, cancellationToken))
        {
            return false;
        }

        await ResumeWaitingAsync(timer.ProcessInstanceId, timer.NodeInstanceId, null, true, null, cancellationToken);
        return true;
    }

    public async Task<int> ProcessDueTimersAsync(int maxCount = 100, CancellationToken cancellationToken = default)
    {
        var fired = 0;
        foreach (var timerId in await GetDueTimersAsync(maxCount, cancellationToken))
        {
            if (await FireTimerAsync(timerId, cancellationToken))
            {
                fired++;
            }
        }

        return fired;
    }

    public async Task<bool> CancelProcessAsync(long processInstanceId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var run = await LoadRunAsync(processInstanceId, forUpdate: true, cancellationToken);
        if (run.Record.Status is ProcessInstanceStatus.Completed or ProcessInstanceStatus.Cancelled)
        {
            return false;
        }

        await CancelRunAsync(run, reason, cancellationToken);
        return true;
    }

    public Task<ProcessInstanceRecord?> GetProcessInstanceAsync(long processInstanceId, CancellationToken cancellationToken = default)
        => _instances.GetAsync(processInstanceId, forUpdate: false, cancellationToken);

    public Task<IReadOnlyList<ProcessInstanceRecord>> FindProcessInstancesAsync(string businessKey, CancellationToken cancellationToken = default)
        => _instances.FindByBusinessKeyAsync(businessKey, cancellationToken);

    public Task<IReadOnlyList<NodeInstanceRecord>> GetHistoryAsync(long processInstanceId, CancellationToken cancellationToken = default)
        => _nodeInstances.GetHistoryAsync(processInstanceId, cancellationToken);

    public async Task<ProcessVariables> GetVariablesAsync(long processInstanceId, CancellationToken cancellationToken = default)
    {
        var record = await _instances.GetAsync(processInstanceId, forUpdate: false, cancellationToken)
                     ?? throw new SentierNotFoundException($"Instance de processus {processInstanceId} introuvable.");
        return ProcessVariables.FromJson(record.Variables);
    }

    private async Task<long> StartAsync(ProcessDefinition definition, string? businessKey, IDictionary<string, object?>? variables, CancellationToken ct)
    {
        var run = await CreateInstanceAsync(definition, businessKey, new ProcessVariables(variables), parent: null, parentNodeInstanceId: null, ct);
        await RunFromAsync(run, definition.GetStartNode(), ct);
        return run.Record.Id;
    }

    private async Task CompleteUserTaskCoreAsync(UserTaskRecord task, UserTaskCompletion? completion, CancellationToken ct)
    {
        if (task.Status != WaitStatus.Pending
            || !await _userTasks.CompleteAsync(task.Id, completion?.CompletedBy, completion?.Outcome, _clock.UtcNow, ct))
        {
            throw new SentierStateException($"La tâche utilisateur {task.Id} n'est plus en attente.");
        }

        var run = await LoadRunAsync(task.ProcessInstanceId, forUpdate: true, ct);
        var nodeInstance = await GetNodeInstanceAsync(task.NodeInstanceId, ct);

        run.Variables.Merge(completion?.Variables);
        if (run.Definition.GetNode(nodeInstance.NodeId) is UserTaskNode { OutcomeVariable: { Length: > 0 } outcomeVariable })
        {
            run.Variables[outcomeVariable] = completion?.Outcome;
        }

        // L'issue choisit la transition si elle porte ce nom, sinon transition par défaut.
        await ContinueAfterWaitAsync(run, nodeInstance, completion?.Outcome, strict: false, ct);
    }
}
