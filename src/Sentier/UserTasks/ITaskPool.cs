namespace Sentier.UserTasks;

/// <summary>
/// Pont vers le bassin de tâches existant du client. Les appels se font pendant l'exécution du processus :
/// l'implémentation peut utiliser la même <see cref="Data.ISentierDbSession"/> pour écrire dans la même transaction.
/// Quand la tâche est traitée, le client appelle <see cref="Execution.IProcessEngine.CompleteUserTaskAsync"/>
/// (ou <see cref="Execution.IProcessEngine.CompleteUserTaskByExternalIdAsync"/>).
/// </summary>
public interface ITaskPool
{
    /// <returns>Identifiant de la tâche dans le bassin (conservé dans USER_TASK.EXTERNAL_ID), ou null.</returns>
    Task<string?> CreateTaskAsync(UserTaskRequest request, CancellationToken cancellationToken);

    /// <summary>Appelé quand l'instance est annulée alors que la tâche est encore ouverte.</summary>
    Task CancelTaskAsync(UserTaskCancellation cancellation, CancellationToken cancellationToken);
}

public sealed class UserTaskRequest
{
    /// <summary>Clé Sentier de la tâche (USER_TASK.ID).</summary>
    public long UserTaskId { get; init; }

    public long ProcessInstanceId { get; init; }

    public long RootProcessInstanceId { get; init; }

    public long NodeInstanceId { get; init; }

    public string DefinitionKey { get; init; } = string.Empty;

    public int DefinitionVersion { get; init; }

    public string? BusinessKey { get; init; }

    public string NodeId { get; init; } = string.Empty;

    public string TaskCode { get; init; } = string.Empty;

    public string? Title { get; init; }

    public IReadOnlyList<string> CandidateGroups { get; init; } = [];

    public string? Assignee { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? DueAt { get; init; }

    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    /// <summary>Copie des variables de l'instance au moment de la création.</summary>
    public IReadOnlyDictionary<string, object?> Variables { get; init; } = new Dictionary<string, object?>();
}

public sealed class UserTaskCancellation
{
    public long UserTaskId { get; init; }

    public string ExternalId { get; init; } = string.Empty;

    public long ProcessInstanceId { get; init; }

    public string? Reason { get; init; }
}

/// <summary>Données de complétion d'une tâche utilisateur.</summary>
public sealed class UserTaskCompletion
{
    public string? CompletedBy { get; init; }

    /// <summary>Issue de la tâche ; si une transition porte ce nom, elle est prise, sinon la transition par défaut.</summary>
    public string? Outcome { get; init; }

    /// <summary>Variables fusionnées dans l'instance avant de continuer.</summary>
    public IDictionary<string, object?>? Variables { get; init; }
}

/// <summary>Implémentation par défaut tant que le client n'a pas enregistré son bassin.</summary>
internal sealed class MissingTaskPool : ITaskPool
{
    private const string Message = "Aucun ITaskPool n'est enregistré : appeler AddTaskPool<T>() lors de AddSentier().";

    public Task<string?> CreateTaskAsync(UserTaskRequest request, CancellationToken cancellationToken)
        => throw new SentierConfigurationException(Message);

    public Task CancelTaskAsync(UserTaskCancellation cancellation, CancellationToken cancellationToken)
        => throw new SentierConfigurationException(Message);
}
