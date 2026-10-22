namespace Sentier.Definitions;

/// <summary>Déploiement et lecture des définitions de processus versionnées.</summary>
public interface IProcessDefinitionService
{
    /// <summary>
    /// Déploie une définition. Si le contenu est identique à la dernière version de la même clé, celle-ci est retournée ;
    /// sinon une nouvelle version est créée. La définition retournée porte <see cref="ProcessDefinition.Id"/> et <see cref="ProcessDefinition.Version"/>.
    /// </summary>
    Task<ProcessDefinition> DeployAsync(ProcessDefinition definition, CancellationToken cancellationToken = default);

    Task<ProcessDefinition?> GetLatestAsync(string key, CancellationToken cancellationToken = default);

    Task<ProcessDefinition?> GetVersionAsync(string key, int version, CancellationToken cancellationToken = default);

    Task<ProcessDefinition?> GetAsync(long definitionId, CancellationToken cancellationToken = default);
}
