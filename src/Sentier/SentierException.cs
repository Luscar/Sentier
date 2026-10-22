namespace Sentier;

/// <summary>Exception de base du moteur Sentier.</summary>
public class SentierException : Exception
{
    public SentierException(string message) : base(message) { }
    public SentierException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Définition de processus invalide.</summary>
public sealed class SentierDefinitionException : SentierException
{
    public SentierDefinitionException(string definitionKey, IReadOnlyList<string> errors)
        : base($"Définition de processus '{definitionKey}' invalide : {string.Join(" | ", errors)}")
    {
        DefinitionKey = definitionKey;
        Errors = errors;
    }

    public string DefinitionKey { get; }
    public IReadOnlyList<string> Errors { get; }
}

/// <summary>Élément introuvable (définition, instance, tâche, etc.).</summary>
public sealed class SentierNotFoundException : SentierException
{
    public SentierNotFoundException(string message) : base(message) { }
}

/// <summary>Opération incompatible avec l'état courant d'une instance ou d'un élément d'attente.</summary>
public sealed class SentierStateException : SentierException
{
    public SentierStateException(string message) : base(message) { }
}

/// <summary>La ligne a été modifiée par une autre transaction (verrou optimiste).</summary>
public sealed class SentierConcurrencyException : SentierException
{
    public SentierConcurrencyException(string message) : base(message) { }
}

/// <summary>Erreur pendant l'exécution d'un noeud.</summary>
public sealed class SentierExecutionException : SentierException
{
    public SentierExecutionException(string message) : base(message) { }
    public SentierExecutionException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Configuration manquante ou incorrecte (handler non enregistré, pool de tâches absent, etc.).</summary>
public sealed class SentierConfigurationException : SentierException
{
    public SentierConfigurationException(string message) : base(message) { }
}
