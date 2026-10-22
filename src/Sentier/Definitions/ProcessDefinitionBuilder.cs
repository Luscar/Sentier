namespace Sentier.Definitions;

/// <summary>
/// Construction fluide d'une définition :
/// <code>
/// var def = ProcessDefinitionBuilder.Create("DEMANDE", "Traitement d'une demande")
///     .Start()
///     .SystemTask("valider", handler: "ValiderDemande")
///     .Decision("conforme", variable: "estConforme")
///     .UserTask("corriger", t => t.CandidateGroups.Add("AGENTS"))
///     .WaitSignal("attendrePaiement", "PaiementRecu", correlateOnBusinessKey: true)
///     .End()
///     .Chain("start", "valider", "conforme")
///     .Transition("conforme", "attendrePaiement", name: "oui")
///     .Transition("conforme", "corriger", name: "non", isDefault: true)
///     .Chain("corriger", "valider")
///     .Chain("attendrePaiement", "end")
///     .Build();
/// </code>
/// </summary>
public sealed class ProcessDefinitionBuilder
{
    private readonly ProcessDefinition _definition;

    private ProcessDefinitionBuilder(string key, string? name)
    {
        _definition = new ProcessDefinition { Key = key, Name = name };
    }

    public static ProcessDefinitionBuilder Create(string key, string? name = null) => new(key, name);

    public ProcessDefinitionBuilder Node(NodeDefinition node)
    {
        _definition.Nodes.Add(node);
        return this;
    }

    public ProcessDefinitionBuilder Start(string id = "start", string? name = null)
        => Node(new StartNode { Id = id, Name = name });

    public ProcessDefinitionBuilder End(string id = "end", string? name = null)
        => Node(new EndNode { Id = id, Name = name });

    public ProcessDefinitionBuilder SystemTask(string id, string handler, string? name = null)
        => Node(new SystemNode { Id = id, Handler = handler, Name = name });

    public ProcessDefinitionBuilder WaitDuration(string id, TimeSpan duration, string? name = null)
        => Node(new TimerWaitNode { Id = id, Duration = duration, Name = name });

    public ProcessDefinitionBuilder WaitDurationFromVariable(string id, string durationVariable, string? name = null)
        => Node(new TimerWaitNode { Id = id, DurationVariable = durationVariable, Name = name });

    public ProcessDefinitionBuilder WaitUntil(string id, string dueDateVariable, string? name = null)
        => Node(new TimerWaitNode { Id = id, DueDateVariable = dueDateVariable, Name = name });

    public ProcessDefinitionBuilder WaitSignal(string id, string signalName, string? correlationVariable = null, bool correlateOnBusinessKey = false, string? name = null)
        => Node(new SignalWaitNode
        {
            Id = id,
            SignalName = signalName,
            CorrelationVariable = correlationVariable,
            CorrelateOnBusinessKey = correlateOnBusinessKey,
            Name = name,
        });

    public ProcessDefinitionBuilder UserTask(string id, Action<UserTaskNode>? configure = null)
    {
        var node = new UserTaskNode { Id = id };
        configure?.Invoke(node);
        return Node(node);
    }

    public ProcessDefinitionBuilder Decision(string id, string? handler = null, string? variable = null, string? name = null)
        => Node(new DecisionNode { Id = id, Handler = handler, Variable = variable, Name = name });

    public ProcessDefinitionBuilder SubProcess(string id, string definitionKey, Action<SubProcessNode>? configure = null)
    {
        var node = new SubProcessNode { Id = id, DefinitionKey = definitionKey };
        configure?.Invoke(node);
        return Node(node);
    }

    public ProcessDefinitionBuilder Transition(string from, string to, string? name = null, bool isDefault = false)
    {
        _definition.Transitions.Add(new TransitionDefinition { From = from, To = to, Name = name, IsDefault = isDefault });
        return this;
    }

    /// <summary>Relie les noeuds dans l'ordre donné par des transitions non nommées.</summary>
    public ProcessDefinitionBuilder Chain(params string[] nodeIds)
    {
        for (var i = 1; i < nodeIds.Length; i++)
        {
            Transition(nodeIds[i - 1], nodeIds[i]);
        }

        return this;
    }

    public ProcessDefinition Build()
    {
        ProcessDefinitionValidator.Validate(_definition);
        return _definition;
    }
}
