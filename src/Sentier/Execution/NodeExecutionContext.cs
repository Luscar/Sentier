using System.Data;
using Sentier.Definitions;

namespace Sentier.Execution;

/// <summary>Contexte passé aux handlers systémiques et de décision.</summary>
public sealed class NodeExecutionContext
{
    internal NodeExecutionContext(
        InstanceRun run,
        NodeDefinition node,
        long nodeInstanceId,
        IDbConnection connection,
        IDbTransaction? transaction,
        DateTime utcNow)
    {
        ProcessInstanceId = run.Record.Id;
        RootProcessInstanceId = run.Record.RootInstanceId;
        ParentProcessInstanceId = run.Record.ParentInstanceId;
        DefinitionKey = run.Definition.Key;
        DefinitionVersion = run.Definition.Version;
        BusinessKey = run.Record.BusinessKey;
        Node = node;
        NodeInstanceId = nodeInstanceId;
        Variables = run.Variables;
        Connection = connection;
        Transaction = transaction;
        UtcNow = utcNow;
    }

    public long ProcessInstanceId { get; }

    public long RootProcessInstanceId { get; }

    public long? ParentProcessInstanceId { get; }

    public string DefinitionKey { get; }

    public int DefinitionVersion { get; }

    public string? BusinessKey { get; }

    public NodeDefinition Node { get; }

    public long NodeInstanceId { get; }

    /// <summary>Variables de l'instance, modifiables ; sauvegardées au prochain point d'arrêt.</summary>
    public ProcessVariables Variables { get; }

    /// <summary>Connexion du client, à utiliser pour que le travail du handler reste dans la même transaction.</summary>
    public IDbConnection Connection { get; }

    public IDbTransaction? Transaction { get; }

    public DateTime UtcNow { get; }

    public string? SelectedTransition { get; private set; }

    /// <summary>Choisit la transition sortante (nom ou identifiant du noeud cible) d'un noeud systémique.</summary>
    public void TakeTransition(string transitionName) => SelectedTransition = transitionName;
}
