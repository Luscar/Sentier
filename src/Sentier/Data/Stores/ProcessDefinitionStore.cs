using Dapper;
using Sentier.Data.Records;

namespace Sentier.Data.Stores;

internal sealed class ProcessDefinitionStore
{
    private readonly SentierDb _db;

    public ProcessDefinitionStore(SentierDb db) => _db = db;

    private string Table => _db.Names.ProcessDefinition;

    private string SelectSql => $@"SELECT ID AS Id, DEF_KEY AS DefinitionKey, VERSION_NO AS VersionNumber, NAME AS Name,
       CONTENT AS Content, CONTENT_HASH AS ContentHash, CREATED_AT AS CreatedAt
FROM {Table}";

    public Task<ProcessDefinitionRecord?> GetAsync(long id, CancellationToken ct)
        => _db.QueryFirstOrDefaultAsync<ProcessDefinitionRecord>($"{SelectSql} WHERE ID = :Id", new { Id = id }, ct);

    public Task<ProcessDefinitionRecord?> GetLatestAsync(string key, CancellationToken ct)
        => _db.QueryFirstOrDefaultAsync<ProcessDefinitionRecord>(
            $"{SelectSql} WHERE DEF_KEY = :Key AND VERSION_NO = (SELECT MAX(VERSION_NO) FROM {Table} WHERE DEF_KEY = :Key)",
            new { Key = key }, ct);

    public Task<ProcessDefinitionRecord?> GetVersionAsync(string key, int version, CancellationToken ct)
        => _db.QueryFirstOrDefaultAsync<ProcessDefinitionRecord>(
            $"{SelectSql} WHERE DEF_KEY = :Key AND VERSION_NO = :Version", new { Key = key, Version = version }, ct);

    public async Task InsertAsync(ProcessDefinitionRecord record, CancellationToken ct)
    {
        record.Id = await _db.NextIdAsync(_db.Names.ProcessDefinitionSequence, ct);

        var p = new DynamicParameters(new
        {
            record.Id,
            record.DefinitionKey,
            record.VersionNumber,
            record.Name,
            record.ContentHash,
            record.CreatedAt,
        });
        p.Add("Content", _db.LargeText(record.Content));

        await _db.ExecuteAsync($@"INSERT INTO {Table} (ID, DEF_KEY, VERSION_NO, NAME, CONTENT, CONTENT_HASH, CREATED_AT)
VALUES (:Id, :DefinitionKey, :VersionNumber, :Name, :Content, :ContentHash, :CreatedAt)", p, ct);
    }
}
