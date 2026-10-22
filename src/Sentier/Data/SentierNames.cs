using System.Text.RegularExpressions;

namespace Sentier.Data;

/// <summary>
/// Noms physiques des objets de base de données, tous préfixés.
/// Les suffixes sont limités à 19 caractères pour respecter la limite Oracle de 30 caractères avec un préfixe de 10.
/// </summary>
public sealed class SentierNames
{
    private static readonly Regex ValidPrefix = new("^[A-Za-z][A-Za-z0-9_]{0,9}$", RegexOptions.Compiled);

    public SentierNames(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix) || !ValidPrefix.IsMatch(prefix))
        {
            throw new ArgumentException(
                $"Préfixe de tables '{prefix}' invalide : 1 à 10 caractères alphanumériques ou '_', commençant par une lettre.",
                nameof(prefix));
        }

        Prefix = prefix.ToUpperInvariant().TrimEnd('_');
    }

    public string Prefix { get; }

    // Tables
    public string ProcessDefinition => Name("PROC_DEF");
    public string ProcessInstance => Name("PROC_INST");
    public string NodeInstance => Name("NODE_INST");
    public string Timer => Name("TIMER");
    public string SignalSubscription => Name("SIGNAL_SUB");
    public string UserTask => Name("USER_TASK");

    /// <summary>Table d'émulation des séquences (SQLite uniquement).</summary>
    public string SequenceTable => Name("SEQUENCE");

    // Vues
    public string WaitingInstancesView => Name("V_INST_ATTENTE");
    public string OpenUserTasksView => Name("V_TACHE_OUVERTE");

    // Séquences
    public string ProcessDefinitionSequence => Name("SEQ_PROC_DEF");
    public string ProcessInstanceSequence => Name("SEQ_PROC_INST");
    public string NodeInstanceSequence => Name("SEQ_NODE_INST");
    public string TimerSequence => Name("SEQ_TIMER");
    public string SignalSubscriptionSequence => Name("SEQ_SIGNAL_SUB");
    public string UserTaskSequence => Name("SEQ_USER_TASK");

    public IReadOnlyList<string> Sequences =>
    [
        ProcessDefinitionSequence,
        ProcessInstanceSequence,
        NodeInstanceSequence,
        TimerSequence,
        SignalSubscriptionSequence,
        UserTaskSequence,
    ];

    /// <summary>Nom préfixé d'un objet (index, contrainte...).</summary>
    public string Name(string suffix) => $"{Prefix}_{suffix}";
}
