using Sentier.Data.Records;
using Sentier.Definitions;

namespace Sentier.Execution;

/// <summary>État en mémoire d'une instance pendant son exécution.</summary>
internal sealed class InstanceRun
{
    public InstanceRun(ProcessInstanceRecord record, ProcessDefinition definition, ProcessVariables variables, int depth)
    {
        Record = record;
        Definition = definition;
        Variables = variables;
        Depth = depth;
    }

    public ProcessInstanceRecord Record { get; }

    public ProcessDefinition Definition { get; }

    public ProcessVariables Variables { get; }

    /// <summary>Profondeur d'imbrication des sous-processus démarrés dans la même exécution.</summary>
    public int Depth { get; }
}

internal enum NodeOutcomeKind
{
    Continue,
    Wait,
    End,
}

internal readonly record struct NodeOutcome(NodeOutcomeKind Kind, string? TransitionName = null, bool Strict = true)
{
    public static NodeOutcome Wait => new(NodeOutcomeKind.Wait);

    public static NodeOutcome End => new(NodeOutcomeKind.End);

    public static NodeOutcome Continue(string? transitionName = null, bool strict = true)
        => new(NodeOutcomeKind.Continue, transitionName, strict);
}
