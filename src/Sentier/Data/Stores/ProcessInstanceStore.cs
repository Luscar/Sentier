using Dapper;
using Sentier.Data.Records;

namespace Sentier.Data.Stores;

internal sealed class ProcessInstanceStore
{
    private readonly SentierDb _db;

    public ProcessInstanceStore(SentierDb db) => _db = db;

    private string Table => _db.Names.ProcessInstance;

    private string SelectSql => $@"SELECT ID AS Id, DEF_ID AS DefinitionId, DEF_KEY AS DefinitionKey, DEF_VERSION AS DefinitionVersion,
       BUSINESS_KEY AS BusinessKey, STATUS AS Status, CURRENT_NODE AS CurrentNodeId, VARIABLES AS Variables,
       PARENT_ID AS ParentInstanceId, PARENT_NODE_INST_ID AS ParentNodeInstanceId, ROOT_ID AS RootInstanceId,
       ROW_VERSION AS RowVersion, CREATED_AT AS CreatedAt, UPDATED_AT AS UpdatedAt, ENDED_AT AS EndedAt
FROM {Table}";

    public Task<long> NextIdAsync(CancellationToken ct) => _db.NextIdAsync(_db.Names.ProcessInstanceSequence, ct);

    /// <param name="forUpdate">Verrouille la ligne (Oracle) pour sérialiser les reprises concurrentes.</param>
    public Task<ProcessInstanceRecord?> GetAsync(long id, bool forUpdate, CancellationToken ct)
        => _db.QueryFirstOrDefaultAsync<ProcessInstanceRecord>(
            $"{SelectSql} WHERE ID = :Id{(forUpdate ? _db.Dialect.ForUpdateClause : string.Empty)}", new { Id = id }, ct);

    public Task<IReadOnlyList<ProcessInstanceRecord>> FindByBusinessKeyAsync(string businessKey, CancellationToken ct)
        => _db.QueryAsync<ProcessInstanceRecord>($"{SelectSql} WHERE BUSINESS_KEY = :BusinessKey ORDER BY ID", new { BusinessKey = businessKey }, ct);

    public Task<IReadOnlyList<long>> GetOpenChildIdsAsync(long parentId, CancellationToken ct)
        => _db.QueryAsync<long>(
            $"SELECT ID FROM {Table} WHERE PARENT_ID = :ParentId AND STATUS IN (:Running, :Waiting) ORDER BY ID",
            new
            {
                ParentId = parentId,
                Running = nameof(ProcessInstanceStatus.Running),
                Waiting = nameof(ProcessInstanceStatus.Waiting),
            }, ct);

    public Task InsertAsync(ProcessInstanceRecord r, CancellationToken ct)
    {
        var p = new DynamicParameters(new
        {
            r.Id,
            r.DefinitionId,
            r.DefinitionKey,
            r.DefinitionVersion,
            r.BusinessKey,
            Status = r.Status.ToString(),
            r.CurrentNodeId,
            r.ParentInstanceId,
            r.ParentNodeInstanceId,
            r.RootInstanceId,
            r.RowVersion,
            r.CreatedAt,
            r.UpdatedAt,
            r.EndedAt,
        });
        p.Add("Variables", _db.LargeText(r.Variables));

        return _db.ExecuteAsync($@"INSERT INTO {Table}
    (ID, DEF_ID, DEF_KEY, DEF_VERSION, BUSINESS_KEY, STATUS, CURRENT_NODE, VARIABLES,
     PARENT_ID, PARENT_NODE_INST_ID, ROOT_ID, ROW_VERSION, CREATED_AT, UPDATED_AT, ENDED_AT)
VALUES
    (:Id, :DefinitionId, :DefinitionKey, :DefinitionVersion, :BusinessKey, :Status, :CurrentNodeId, :Variables,
     :ParentInstanceId, :ParentNodeInstanceId, :RootInstanceId, :RowVersion, :CreatedAt, :UpdatedAt, :EndedAt)", p, ct);
    }

    /// <summary>Mise à jour avec verrou optimiste ; incrémente <see cref="ProcessInstanceRecord.RowVersion"/> si elle réussit.</summary>
    public async Task UpdateAsync(ProcessInstanceRecord r, CancellationToken ct)
    {
        var p = new DynamicParameters(new
        {
            r.Id,
            Status = r.Status.ToString(),
            r.CurrentNodeId,
            r.UpdatedAt,
            r.EndedAt,
            r.RowVersion,
        });
        p.Add("Variables", _db.LargeText(r.Variables));

        var rows = await _db.ExecuteAsync($@"UPDATE {Table}
SET STATUS = :Status, CURRENT_NODE = :CurrentNodeId, VARIABLES = :Variables,
    UPDATED_AT = :UpdatedAt, ENDED_AT = :EndedAt, ROW_VERSION = ROW_VERSION + 1
WHERE ID = :Id AND ROW_VERSION = :RowVersion", p, ct);

        if (rows != 1)
        {
            throw new SentierConcurrencyException($"L'instance de processus {r.Id} a été modifiée par une autre transaction.");
        }

        r.RowVersion++;
    }
}
