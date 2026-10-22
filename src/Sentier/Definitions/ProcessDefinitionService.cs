using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Sentier.Configuration;
using Sentier.Data.Records;
using Sentier.Data.Stores;

namespace Sentier.Definitions;

/// <summary>
/// Cache des définitions désérialisées, indexé par clé technique. Une version déployée étant immuable,
/// l'entrée n'a jamais besoin d'être invalidée.
/// </summary>
internal sealed class ProcessDefinitionCache
{
    private readonly ConcurrentDictionary<long, ProcessDefinition> _byId = new();

    public bool TryGet(long id, [MaybeNullWhen(false)] out ProcessDefinition definition) => _byId.TryGetValue(id, out definition);

    public ProcessDefinition GetOrAdd(ProcessDefinitionRecord record)
        => _byId.GetOrAdd(record.Id, _ => ToDefinition(record));

    public static ProcessDefinition ToDefinition(ProcessDefinitionRecord record)
    {
        var definition = ProcessDefinitionSerializer.Deserialize(record.Content);
        definition.Id = record.Id;
        definition.Version = record.VersionNumber;
        return definition;
    }
}

internal sealed class ProcessDefinitionService : IProcessDefinitionService
{
    private readonly ProcessDefinitionStore _store;
    private readonly ProcessDefinitionCache _cache;
    private readonly ISentierClock _clock;

    public ProcessDefinitionService(ProcessDefinitionStore store, ProcessDefinitionCache cache, ISentierClock clock)
    {
        _store = store;
        _cache = cache;
        _clock = clock;
    }

    public async Task<ProcessDefinition> DeployAsync(ProcessDefinition definition, CancellationToken cancellationToken = default)
    {
        ProcessDefinitionValidator.Validate(definition);

        var content = ProcessDefinitionSerializer.Serialize(definition);
        var hash = ProcessDefinitionSerializer.ComputeHash(content);

        var latest = await _store.GetLatestAsync(definition.Key, cancellationToken);
        if (latest is not null && latest.ContentHash == hash)
        {
            return _cache.GetOrAdd(latest);
        }

        var record = new ProcessDefinitionRecord
        {
            DefinitionKey = definition.Key,
            VersionNumber = (latest?.VersionNumber ?? 0) + 1,
            Name = definition.Name,
            Content = content,
            ContentHash = hash,
            CreatedAt = _clock.UtcNow,
        };
        await _store.InsertAsync(record, cancellationToken);

        // Pas de mise en cache ici : la transaction du client peut encore être annulée.
        return ProcessDefinitionCache.ToDefinition(record);
    }

    public async Task<ProcessDefinition?> GetLatestAsync(string key, CancellationToken cancellationToken = default)
    {
        var record = await _store.GetLatestAsync(key, cancellationToken);
        return record is null ? null : _cache.GetOrAdd(record);
    }

    public async Task<ProcessDefinition?> GetVersionAsync(string key, int version, CancellationToken cancellationToken = default)
    {
        var record = await _store.GetVersionAsync(key, version, cancellationToken);
        return record is null ? null : _cache.GetOrAdd(record);
    }

    public async Task<ProcessDefinition?> GetAsync(long definitionId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGet(definitionId, out var cached))
        {
            return cached;
        }

        var record = await _store.GetAsync(definitionId, cancellationToken);
        return record is null ? null : _cache.GetOrAdd(record);
    }
}
