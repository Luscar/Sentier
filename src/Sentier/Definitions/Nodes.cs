using System.Text.Json.Serialization;

namespace Sentier.Definitions;

public enum NodeType
{
    Start,

    /// <summary>Noeud systémique : exécute un handler applicatif et continue.</summary>
    System,

    /// <summary>Attente temporelle (durée ou date d'échéance) : suspend l'instance.</summary>
    TimerWait,

    /// <summary>Attente d'un signal externe : suspend l'instance.</summary>
    SignalWait,

    /// <summary>Tâche interactive confiée au bassin de tâches du client : suspend l'instance.</summary>
    UserTask,

    /// <summary>Décision : choisit une transition sortante.</summary>
    Decision,

    /// <summary>Démarre un sous-processus et attend sa fin.</summary>
    SubProcess,

    End,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(StartNode), "start")]
[JsonDerivedType(typeof(SystemNode), "system")]
[JsonDerivedType(typeof(TimerWaitNode), "timer")]
[JsonDerivedType(typeof(SignalWaitNode), "signal")]
[JsonDerivedType(typeof(UserTaskNode), "userTask")]
[JsonDerivedType(typeof(DecisionNode), "decision")]
[JsonDerivedType(typeof(SubProcessNode), "subProcess")]
[JsonDerivedType(typeof(EndNode), "end")]
public abstract class NodeDefinition
{
    /// <summary>Identifiant du noeud, unique dans la définition (max 100 caractères).</summary>
    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }

    [JsonIgnore]
    public abstract NodeType Type { get; }

    /// <summary>Vrai pour les noeuds qui suspendent l'exécution.</summary>
    [JsonIgnore]
    public virtual bool IsWaitState => false;
}

public sealed class StartNode : NodeDefinition
{
    public override NodeType Type => NodeType.Start;
}

public sealed class EndNode : NodeDefinition
{
    public override NodeType Type => NodeType.End;
}

public sealed class SystemNode : NodeDefinition
{
    public override NodeType Type => NodeType.System;

    /// <summary>Nom du <see cref="Handlers.ISystemTaskHandler"/> enregistré.</summary>
    public string Handler { get; set; } = string.Empty;
}

/// <summary>
/// Attente temporelle. Priorité : <see cref="DueDateVariable"/>, puis <see cref="DurationVariable"/>, puis <see cref="Duration"/>.
/// Si l'échéance est déjà passée, l'exécution continue immédiatement.
/// </summary>
public sealed class TimerWaitNode : NodeDefinition
{
    public override NodeType Type => NodeType.TimerWait;

    public override bool IsWaitState => true;

    public TimeSpan? Duration { get; set; }

    /// <summary>Variable contenant une durée (TimeSpan).</summary>
    public string? DurationVariable { get; set; }

    /// <summary>Variable contenant une date d'échéance (UTC si non spécifié).</summary>
    public string? DueDateVariable { get; set; }
}

/// <summary>Attente d'un signal nommé, éventuellement corrélé par une clé.</summary>
public sealed class SignalWaitNode : NodeDefinition
{
    public override NodeType Type => NodeType.SignalWait;

    public override bool IsWaitState => true;

    public string SignalName { get; set; } = string.Empty;

    /// <summary>Variable dont la valeur sert de clé de corrélation.</summary>
    public string? CorrelationVariable { get; set; }

    /// <summary>Si aucune variable n'est indiquée, utiliser la clé métier de l'instance comme clé de corrélation.</summary>
    public bool CorrelateOnBusinessKey { get; set; }
}

/// <summary>Tâche interactive créée dans le bassin de tâches du client (<see cref="UserTasks.ITaskPool"/>).</summary>
public sealed class UserTaskNode : NodeDefinition
{
    public override NodeType Type => NodeType.UserTask;

    public override bool IsWaitState => true;

    /// <summary>Code fonctionnel de la tâche (par défaut l'identifiant du noeud).</summary>
    public string? TaskCode { get; set; }

    public string? Title { get; set; }

    public List<string> CandidateGroups { get; set; } = [];

    public string? Assignee { get; set; }

    /// <summary>Variable contenant l'assigné (prioritaire sur <see cref="Assignee"/>).</summary>
    public string? AssigneeVariable { get; set; }

    /// <summary>Délai de traitement attendu, transmis au bassin sous forme d'échéance.</summary>
    public TimeSpan? DueIn { get; set; }

    /// <summary>Si renseignée, l'issue (outcome) de la tâche y est copiée à la complétion.</summary>
    public string? OutcomeVariable { get; set; }

    /// <summary>Propriétés libres transmises telles quelles au bassin de tâches.</summary>
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Décision : le nom de la transition est fourni par un <see cref="Handlers.IDecisionHandler"/> (<see cref="Handler"/>)
/// ou lu dans une variable (<see cref="Variable"/>). Sans correspondance, la transition par défaut est prise.
/// </summary>
public sealed class DecisionNode : NodeDefinition
{
    public override NodeType Type => NodeType.Decision;

    public string? Handler { get; set; }

    public string? Variable { get; set; }
}

/// <summary>
/// Sous-processus : démarre la dernière version de <see cref="DefinitionKey"/>.
/// Les mappages sont de la forme cible ← source ; null signifie « tout copier », vide « ne rien copier ».
/// </summary>
public sealed class SubProcessNode : NodeDefinition
{
    public override NodeType Type => NodeType.SubProcess;

    public override bool IsWaitState => true;

    public string DefinitionKey { get; set; } = string.Empty;

    /// <summary>Clé = variable du sous-processus, valeur = variable du parent.</summary>
    public Dictionary<string, string>? Inputs { get; set; }

    /// <summary>Clé = variable du parent, valeur = variable du sous-processus.</summary>
    public Dictionary<string, string>? Outputs { get; set; }
}
