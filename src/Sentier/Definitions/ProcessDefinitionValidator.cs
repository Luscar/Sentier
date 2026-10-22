namespace Sentier.Definitions;

public static class ProcessDefinitionValidator
{
    private const int MaxIdLength = 100;

    public static IReadOnlyList<string> GetErrors(ProcessDefinition definition)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Key))
        {
            errors.Add("La clé du processus est obligatoire.");
        }
        else if (definition.Key.Length > MaxIdLength)
        {
            errors.Add($"La clé du processus dépasse {MaxIdLength} caractères.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in definition.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                errors.Add("Un noeud n'a pas d'identifiant.");
                continue;
            }

            if (node.Id.Length > MaxIdLength)
            {
                errors.Add($"L'identifiant du noeud '{node.Id}' dépasse {MaxIdLength} caractères.");
            }

            if (!ids.Add(node.Id))
            {
                errors.Add($"Identifiant de noeud en double : '{node.Id}'.");
            }
        }

        var starts = definition.Nodes.OfType<StartNode>().Count();
        if (starts != 1)
        {
            errors.Add($"Un processus doit avoir exactement un noeud de départ ({starts} trouvé(s)).");
        }

        if (!definition.Nodes.OfType<EndNode>().Any())
        {
            errors.Add("Un processus doit avoir au moins un noeud de fin.");
        }

        foreach (var t in definition.Transitions)
        {
            if (!ids.Contains(t.From))
            {
                errors.Add($"Transition depuis un noeud inconnu : '{t.From}'.");
            }

            if (!ids.Contains(t.To))
            {
                errors.Add($"Transition vers un noeud inconnu : '{t.To}'.");
            }
        }

        foreach (var group in definition.Transitions.GroupBy(t => t.From, StringComparer.Ordinal))
        {
            foreach (var dup in group.Where(t => t.Name is not null).GroupBy(t => t.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                errors.Add($"Nom de transition '{dup.Key}' en double depuis le noeud '{group.Key}'.");
            }

            if (group.Count(t => t.IsDefault) > 1)
            {
                errors.Add($"Plusieurs transitions par défaut depuis le noeud '{group.Key}'.");
            }
        }

        foreach (var node in definition.Nodes.Where(n => !string.IsNullOrWhiteSpace(n.Id)))
        {
            var outgoing = definition.Transitions.Where(t => t.From == node.Id).ToList();
            var incoming = definition.Transitions.Count(t => t.To == node.Id);

            switch (node)
            {
                case EndNode when outgoing.Count > 0:
                    errors.Add($"Le noeud de fin '{node.Id}' ne doit pas avoir de transition sortante.");
                    break;
                case EndNode:
                    break;
                case StartNode when incoming > 0:
                    errors.Add($"Le noeud de départ '{node.Id}' ne doit pas avoir de transition entrante.");
                    break;
                default:
                    if (outgoing.Count == 0)
                    {
                        errors.Add($"Le noeud '{node.Id}' n'a aucune transition sortante.");
                    }

                    break;
            }

            if (node is not StartNode && incoming == 0)
            {
                errors.Add($"Le noeud '{node.Id}' est inaccessible (aucune transition entrante).");
            }

            if (outgoing.Count > 1 && node is not (DecisionNode or SystemNode or UserTaskNode))
            {
                errors.Add($"Le noeud '{node.Id}' ({node.Type}) ne peut avoir qu'une seule transition sortante.");
            }

            switch (node)
            {
                case SystemNode s when string.IsNullOrWhiteSpace(s.Handler):
                    errors.Add($"Le noeud systémique '{node.Id}' doit indiquer un handler.");
                    break;
                case DecisionNode d when string.IsNullOrWhiteSpace(d.Handler) && string.IsNullOrWhiteSpace(d.Variable):
                    errors.Add($"La décision '{node.Id}' doit indiquer un handler ou une variable.");
                    break;
                case DecisionNode when outgoing.Count < 2:
                    errors.Add($"La décision '{node.Id}' doit avoir au moins deux transitions sortantes.");
                    break;
                case TimerWaitNode tw when tw.Duration is null && string.IsNullOrWhiteSpace(tw.DurationVariable) && string.IsNullOrWhiteSpace(tw.DueDateVariable):
                    errors.Add($"L'attente temporelle '{node.Id}' doit indiquer une durée, une variable de durée ou une variable d'échéance.");
                    break;
                case SignalWaitNode sw when string.IsNullOrWhiteSpace(sw.SignalName):
                    errors.Add($"L'attente de signal '{node.Id}' doit indiquer un nom de signal.");
                    break;
                case SubProcessNode sp when string.IsNullOrWhiteSpace(sp.DefinitionKey):
                    errors.Add($"Le sous-processus '{node.Id}' doit indiquer la clé du processus à démarrer.");
                    break;
            }
        }

        return errors;
    }

    public static void Validate(ProcessDefinition definition)
    {
        var errors = GetErrors(definition);
        if (errors.Count > 0)
        {
            throw new SentierDefinitionException(definition.Key, errors);
        }
    }
}
