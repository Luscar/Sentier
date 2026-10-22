using System.Text.Json.Serialization;

namespace Sentier.Definitions;

public sealed class TransitionDefinition
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    /// <summary>Nom utilisé par les décisions, les handlers et les issues de tâches (unique par noeud source).</summary>
    public string? Name { get; set; }

    /// <summary>Transition prise lorsqu'aucune transition nommée ne correspond.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Identifiant enregistré dans l'historique des noeuds.</summary>
    [JsonIgnore]
    public string Id => Name ?? $"{From}->{To}";
}

/// <summary>
/// Définition d'un processus : graphe de noeuds reliés par des transitions, exécuté en séquence (un seul jeton).
/// Une fois déployée, elle est immuable et identifiée par une clé technique long et un numéro de version.
/// </summary>
public sealed class ProcessDefinition
{
    private Dictionary<string, NodeDefinition>? _nodesById;
    private Dictionary<string, List<TransitionDefinition>>? _outgoing;

    public string Key { get; set; } = string.Empty;

    public string? Name { get; set; }

    public List<NodeDefinition> Nodes { get; set; } = [];

    public List<TransitionDefinition> Transitions { get; set; } = [];

    /// <summary>Clé technique attribuée au déploiement (0 si non déployée).</summary>
    [JsonIgnore]
    public long Id { get; internal set; }

    /// <summary>Version attribuée au déploiement (0 si non déployée).</summary>
    [JsonIgnore]
    public int Version { get; internal set; }

    public NodeDefinition GetNode(string nodeId)
    {
        _nodesById ??= Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        return _nodesById.TryGetValue(nodeId, out var node)
            ? node
            : throw new SentierExecutionException($"Noeud '{nodeId}' introuvable dans le processus '{Key}' v{Version}.");
    }

    public StartNode GetStartNode()
        => Nodes.OfType<StartNode>().SingleOrDefault()
           ?? throw new SentierExecutionException($"Le processus '{Key}' n'a pas de noeud de départ unique.");

    public IReadOnlyList<TransitionDefinition> GetOutgoing(string nodeId)
    {
        _outgoing ??= Transitions
            .GroupBy(t => t.From, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        return _outgoing.TryGetValue(nodeId, out var list) ? list : [];
    }

    /// <summary>
    /// Choisit la transition sortante d'un noeud. Un nom correspond au <see cref="TransitionDefinition.Name"/>
    /// ou à l'identifiant du noeud cible. Sans nom (ou sans correspondance si <paramref name="strict"/> est faux) :
    /// l'unique transition sortante, sinon la transition par défaut.
    /// </summary>
    public TransitionDefinition ResolveOutgoing(NodeDefinition node, string? name, bool strict)
    {
        var outgoing = GetOutgoing(node.Id);

        if (!string.IsNullOrEmpty(name))
        {
            var match = outgoing.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal))
                        ?? outgoing.FirstOrDefault(t => string.Equals(t.To, name, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }

            if (strict)
            {
                throw new SentierExecutionException($"Transition '{name}' introuvable depuis le noeud '{node.Id}' du processus '{Key}'.");
            }
        }

        if (outgoing.Count == 1)
        {
            return outgoing[0];
        }

        return outgoing.FirstOrDefault(t => t.IsDefault)
               ?? throw new SentierExecutionException(
                   $"Impossible de choisir une transition depuis le noeud '{node.Id}' du processus '{Key}' (valeur '{name}', aucune transition par défaut).");
    }
}
