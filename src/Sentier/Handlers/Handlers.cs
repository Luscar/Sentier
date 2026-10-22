using Microsoft.Extensions.DependencyInjection;
using Sentier.Execution;

namespace Sentier.Handlers;

/// <summary>Traitement applicatif d'un noeud systémique, exécuté dans la transaction du client.</summary>
public interface ISystemTaskHandler
{
    /// <remarks>
    /// Le handler peut lire et modifier <see cref="NodeExecutionContext.Variables"/> et choisir la transition sortante
    /// via <see cref="NodeExecutionContext.TakeTransition"/>. Une exception interrompt l'exécution : le client doit annuler sa transaction.
    /// </remarks>
    Task ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken);
}

/// <summary>Logique d'un noeud de décision.</summary>
public interface IDecisionHandler
{
    /// <returns>Nom de la transition (ou identifiant du noeud cible) ; null pour la transition par défaut.</returns>
    Task<string?> DecideAsync(NodeExecutionContext context, CancellationToken cancellationToken);
}

/// <summary>Résolution des handlers par nom.</summary>
public interface IHandlerResolver
{
    ISystemTaskHandler GetSystemTask(string name);

    IDecisionHandler GetDecision(string name);
}

/// <summary>Résolution via les services à clé (keyed services) du conteneur d'injection.</summary>
internal sealed class ServiceProviderHandlerResolver : IHandlerResolver
{
    private readonly IServiceProvider _services;

    public ServiceProviderHandlerResolver(IServiceProvider services) => _services = services;

    public ISystemTaskHandler GetSystemTask(string name)
        => _services.GetKeyedService<ISystemTaskHandler>(name)
           ?? throw new SentierConfigurationException($"Aucun handler systémique enregistré sous le nom '{name}'.");

    public IDecisionHandler GetDecision(string name)
        => _services.GetKeyedService<IDecisionHandler>(name)
           ?? throw new SentierConfigurationException($"Aucun handler de décision enregistré sous le nom '{name}'.");
}

internal sealed class DelegateSystemTaskHandler : ISystemTaskHandler
{
    private readonly Func<NodeExecutionContext, CancellationToken, Task> _execute;

    public DelegateSystemTaskHandler(Func<NodeExecutionContext, CancellationToken, Task> execute) => _execute = execute;

    public Task ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken) => _execute(context, cancellationToken);
}

internal sealed class DelegateDecisionHandler : IDecisionHandler
{
    private readonly Func<NodeExecutionContext, CancellationToken, Task<string?>> _decide;

    public DelegateDecisionHandler(Func<NodeExecutionContext, CancellationToken, Task<string?>> decide) => _decide = decide;

    public Task<string?> DecideAsync(NodeExecutionContext context, CancellationToken cancellationToken) => _decide(context, cancellationToken);
}
