using Sentier.Data.Records;

namespace Sentier.Data.Stores;

internal sealed class NodeInstanceStore
{
    private readonly SentierDb _db;

    public NodeInstanceStore(SentierDb db) => _db = db;

    private string Table => _db.Names.NodeInstance;

    private string SelectSql => $@"SELECT ID AS Id, PROC_INST_ID AS ProcessInstanceId, NODE_ID AS NodeId, NODE_TYPE AS NodeType,
       STATUS AS Status, TRANSITION_ID AS TransitionId, STARTED_AT AS StartedAt, ENDED_AT AS EndedAt
FROM {Table}";

    public Task<NodeInstanceRecord?> GetAsync(long id, CancellationToken ct)
        => _db.QueryFirstOrDefaultAsync<NodeInstanceRecord>($"{SelectSql} WHERE ID = :Id", new { Id = id }, ct);

    public Task<IReadOnlyList<NodeInstanceRecord>> GetHistoryAsync(long processInstanceId, CancellationToken ct)
        => _db.QueryAsync<NodeInstanceRecord>($"{SelectSql} WHERE PROC_INST_ID = :ProcessInstanceId ORDER BY ID", new { ProcessInstanceId = processInstanceId }, ct);

    public async Task InsertAsync(NodeInstanceRecord r, CancellationToken ct)
    {
        r.Id = await _db.NextIdAsync(_db.Names.NodeInstanceSequence, ct);

        await _db.ExecuteAsync($@"INSERT INTO {Table} (ID, PROC_INST_ID, NODE_ID, NODE_TYPE, STATUS, TRANSITION_ID, STARTED_AT, ENDED_AT)
VALUES (:Id, :ProcessInstanceId, :NodeId, :NodeType, :Status, :TransitionId, :StartedAt, :EndedAt)",
            new
            {
                r.Id,
                r.ProcessInstanceId,
                r.NodeId,
                r.NodeType,
                Status = r.Status.ToString(),
                r.TransitionId,
                r.StartedAt,
                r.EndedAt,
            }, ct);
    }

    public Task SetWaitingAsync(long id, CancellationToken ct)
        => _db.ExecuteAsync($"UPDATE {Table} SET STATUS = :Status WHERE ID = :Id",
            new { Id = id, Status = nameof(NodeInstanceStatus.Waiting) }, ct);

    public Task CompleteAsync(long id, string? transitionId, DateTime endedAt, CancellationToken ct)
        => _db.ExecuteAsync($"UPDATE {Table} SET STATUS = :Status, TRANSITION_ID = :TransitionId, ENDED_AT = :EndedAt WHERE ID = :Id",
            new { Id = id, Status = nameof(NodeInstanceStatus.Completed), TransitionId = transitionId, EndedAt = endedAt }, ct);

    public Task CancelOpenAsync(long processInstanceId, DateTime endedAt, CancellationToken ct)
        => _db.ExecuteAsync($@"UPDATE {Table} SET STATUS = :Cancelled, ENDED_AT = :EndedAt
WHERE PROC_INST_ID = :ProcessInstanceId AND STATUS IN (:Active, :Waiting)",
            new
            {
                ProcessInstanceId = processInstanceId,
                EndedAt = endedAt,
                Cancelled = nameof(NodeInstanceStatus.Cancelled),
                Active = nameof(NodeInstanceStatus.Active),
                Waiting = nameof(NodeInstanceStatus.Waiting),
            }, ct);
}
