using Sentier.Data.Records;
using Sentier.UserTasks;

namespace Sentier.Execution;

/// <summary>
/// Moteur d'exécution. Chaque appel exécute les noeuds l'un à la suite de l'autre jusqu'à un noeud d'attente
/// (temporelle, signal, tâche utilisateur, sous-processus suspendu) ou de fin, entièrement dans la transaction
/// fournie par le client via <see cref="Data.ISentierDbSession"/>. Le client valide ou annule la transaction.
/// </summary>
public interface IProcessEngine
{
    /// <summary>Démarre la dernière version déployée du processus.</summary>
    /// <returns>Clé de l'instance créée.</returns>
    Task<long> StartProcessAsync(string definitionKey, string? businessKey = null, IDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);

    /// <summary>Démarre une version précise d'une définition (clé technique).</summary>
    Task<long> StartProcessByIdAsync(long definitionId, string? businessKey = null, IDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);

    /// <summary>Complète une tâche utilisateur et poursuit l'exécution de l'instance.</summary>
    Task CompleteUserTaskAsync(long userTaskId, UserTaskCompletion? completion = null, CancellationToken cancellationToken = default);

    /// <summary>Complète une tâche à partir de l'identifiant du bassin de tâches.</summary>
    Task CompleteUserTaskByExternalIdAsync(string externalTaskId, UserTaskCompletion? completion = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Envoie un signal. Avec une clé de corrélation, seules les attentes de cette clé sont réveillées ;
    /// sans clé, toutes les attentes de ce signal le sont (diffusion).
    /// </summary>
    /// <returns>Nombre d'instances reprises.</returns>
    Task<int> SendSignalAsync(string signalName, string? correlationKey = null, IDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);

    /// <summary>Minuteries échues, pour un traitement unitaire (une transaction par minuterie) via <see cref="FireTimerAsync"/>.</summary>
    Task<IReadOnlyList<long>> GetDueTimersAsync(int maxCount = 100, CancellationToken cancellationToken = default);

    /// <summary>Déclenche une minuterie ; false si elle n'est plus en attente.</summary>
    Task<bool> FireTimerAsync(long timerId, CancellationToken cancellationToken = default);

    /// <summary>Déclenche les minuteries échues dans la transaction courante.</summary>
    Task<int> ProcessDueTimersAsync(int maxCount = 100, CancellationToken cancellationToken = default);

    /// <summary>Annule une instance, ses sous-processus ouverts, ses attentes et ses tâches.</summary>
    /// <returns>false si l'instance était déjà terminée ou annulée.</returns>
    Task<bool> CancelProcessAsync(long processInstanceId, string? reason = null, CancellationToken cancellationToken = default);

    Task<ProcessInstanceRecord?> GetProcessInstanceAsync(long processInstanceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessInstanceRecord>> FindProcessInstancesAsync(string businessKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeInstanceRecord>> GetHistoryAsync(long processInstanceId, CancellationToken cancellationToken = default);

    Task<ProcessVariables> GetVariablesAsync(long processInstanceId, CancellationToken cancellationToken = default);
}
