namespace Sentier.Data.Records;

// Les statuts sont persistés sous forme de chaîne (nom de l'enum).

public enum ProcessInstanceStatus
{
    /// <summary>En cours d'exécution dans la transaction courante.</summary>
    Running,

    /// <summary>Suspendue sur un noeud d'attente, une tâche utilisateur ou un sous-processus.</summary>
    Waiting,

    Completed,
    Cancelled,
}

public enum NodeInstanceStatus
{
    Active,
    Waiting,
    Completed,
    Cancelled,
}

/// <summary>Statut d'un élément d'attente : minuterie, abonnement à un signal, tâche utilisateur.</summary>
public enum WaitStatus
{
    Pending,
    Completed,
    Cancelled,
}

public sealed class ProcessDefinitionRecord
{
    public long Id { get; set; }
    public string DefinitionKey { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string? Name { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ProcessInstanceRecord
{
    public long Id { get; set; }
    public long DefinitionId { get; set; }
    public string DefinitionKey { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; }
    public string? BusinessKey { get; set; }
    public ProcessInstanceStatus Status { get; set; }
    public string? CurrentNodeId { get; set; }
    public string? Variables { get; set; }
    public long? ParentInstanceId { get; set; }
    public long? ParentNodeInstanceId { get; set; }
    public long RootInstanceId { get; set; }
    public int RowVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class NodeInstanceRecord
{
    public long Id { get; set; }
    public long ProcessInstanceId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public NodeInstanceStatus Status { get; set; }
    public string? TransitionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public sealed class TimerRecord
{
    public long Id { get; set; }
    public long ProcessInstanceId { get; set; }
    public long NodeInstanceId { get; set; }
    public DateTime DueAt { get; set; }
    public WaitStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FiredAt { get; set; }
}

public sealed class SignalSubscriptionRecord
{
    public long Id { get; set; }
    public long ProcessInstanceId { get; set; }
    public long NodeInstanceId { get; set; }
    public string SignalName { get; set; } = string.Empty;
    public string? CorrelationKey { get; set; }
    public WaitStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
}

public sealed class UserTaskRecord
{
    public long Id { get; set; }
    public long ProcessInstanceId { get; set; }
    public long NodeInstanceId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public WaitStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public string? Outcome { get; set; }
}
