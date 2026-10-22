using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sentier.Configuration;
using Sentier.Data;
using Sentier.Data.Dialects;
using Sentier.Data.Stores;
using Sentier.Definitions;
using Sentier.Execution;
using Sentier.Handlers;
using Sentier.UserTasks;

namespace Sentier.DependencyInjection;

public static class SentierServiceCollectionExtensions
{
    /// <summary>
    /// Enregistre le moteur. Le client doit aussi fournir sa connexion/transaction
    /// (<see cref="SentierBuilder.UseDbSession{TSession}"/> ou <see cref="SentierBuilder.UseConnection"/>)
    /// et, s'il utilise des tâches utilisateurs, son bassin de tâches (<see cref="SentierBuilder.AddTaskPool{TTaskPool}"/>).
    /// </summary>
    public static SentierBuilder AddSentier(this IServiceCollection services, Action<SentierOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<SentierOptions>().Configure(configure);

        services.TryAddSingleton<ISentierClock, SystemSentierClock>();
        services.TryAddSingleton(sp => new SentierNames(sp.GetRequiredService<IOptions<SentierOptions>>().Value.TablePrefix));
        services.TryAddSingleton(sp => SqlDialects.Create(
            sp.GetRequiredService<IOptions<SentierOptions>>().Value.Database,
            sp.GetRequiredService<SentierNames>()));
        services.TryAddSingleton<ProcessDefinitionCache>();

        services.TryAddScoped<SentierDb>();
        services.TryAddScoped<ProcessDefinitionStore>();
        services.TryAddScoped<ProcessInstanceStore>();
        services.TryAddScoped<NodeInstanceStore>();
        services.TryAddScoped<TimerStore>();
        services.TryAddScoped<SignalSubscriptionStore>();
        services.TryAddScoped<UserTaskStore>();

        services.TryAddScoped<IHandlerResolver, ServiceProviderHandlerResolver>();
        services.TryAddScoped<ITaskPool, MissingTaskPool>();
        services.TryAddScoped<IProcessDefinitionService, ProcessDefinitionService>();
        services.TryAddScoped<IProcessEngine, ProcessEngine>();

        return new SentierBuilder(services);
    }
}

public sealed class SentierBuilder
{
    internal SentierBuilder(IServiceCollection services) => Services = services;

    public IServiceCollection Services { get; }

    /// <summary>Session (connexion + transaction) fournie par une classe du client, résolue par scope.</summary>
    public SentierBuilder UseDbSession<TSession>() where TSession : class, ISentierDbSession
    {
        Services.Replace(ServiceDescriptor.Scoped<ISentierDbSession, TSession>());
        return this;
    }

    public SentierBuilder UseDbSession(Func<IServiceProvider, ISentierDbSession> factory)
    {
        Services.Replace(ServiceDescriptor.Scoped(factory));
        return this;
    }

    /// <summary>
    /// Session construite à partir de la connexion et de la transaction déjà gérées par le client.
    /// Les fonctions sont évaluées à chaque commande, la transaction peut donc être ouverte après la création du scope.
    /// </summary>
    public SentierBuilder UseConnection(Func<IServiceProvider, IDbConnection> connection, Func<IServiceProvider, IDbTransaction?>? transaction = null)
    {
        Services.Replace(ServiceDescriptor.Scoped<ISentierDbSession>(sp =>
            new DelegateSentierDbSession(() => connection(sp), () => transaction?.Invoke(sp))));
        return this;
    }

    public SentierBuilder AddTaskPool<TTaskPool>() where TTaskPool : class, ITaskPool
    {
        Services.Replace(ServiceDescriptor.Scoped<ITaskPool, TTaskPool>());
        return this;
    }

    public SentierBuilder AddSystemTask<THandler>(string name) where THandler : class, ISystemTaskHandler
    {
        Services.AddKeyedScoped<ISystemTaskHandler, THandler>(name);
        return this;
    }

    public SentierBuilder AddSystemTask(string name, Func<NodeExecutionContext, CancellationToken, Task> execute)
    {
        Services.AddKeyedSingleton<ISystemTaskHandler>(name, new DelegateSystemTaskHandler(execute));
        return this;
    }

    public SentierBuilder AddDecision<THandler>(string name) where THandler : class, IDecisionHandler
    {
        Services.AddKeyedScoped<IDecisionHandler, THandler>(name);
        return this;
    }

    public SentierBuilder AddDecision(string name, Func<NodeExecutionContext, CancellationToken, Task<string?>> decide)
    {
        Services.AddKeyedSingleton<IDecisionHandler>(name, new DelegateDecisionHandler(decide));
        return this;
    }

    public SentierBuilder UseClock<TClock>() where TClock : class, ISentierClock
    {
        Services.Replace(ServiceDescriptor.Singleton<ISentierClock, TClock>());
        return this;
    }
}
