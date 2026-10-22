using Sentier.Data.Records;

namespace Sentier.Data.Stores;

internal sealed class TimerStore
{
    private readonly SentierDb _db;

    public TimerStore(SentierDb db) => _db = db;

    private string Table => _db.Names.Timer;

    public Task<TimerRecord?> GetAsync(long id, CancellationToken ct)
        => _db.QueryFirstOrDefaultAsync<TimerRecord>($@"SELECT ID AS Id, PROC_INST_ID AS ProcessInstanceId, NODE_INST_ID AS NodeInstanceId,
       DUE_AT AS DueAt, STATUS AS Status, CREATED_AT AS CreatedAt, FIRED_AT AS FiredAt
FROM {Table} WHERE ID = :Id", new { Id = id }, ct);

    public Task<IReadOnlyList<long>> GetDueIdsAsync(DateTime now, int maxCount, CancellationToken ct)
        => _db.QueryAsync<long>(
            _db.Dialect.Limit($"SELECT ID FROM {Table} WHERE STATUS = :Pending AND DUE_AT <= :Now ORDER BY DUE_AT, ID", maxCount),
            new { Now = now, Pending = nameof(WaitStatus.Pending) }, ct);

    public async Task InsertAsync(TimerRecord r, CancellationToken ct)
    {
        r.Id = await _db.NextIdAsync(_db.Names.TimerSequence, ct);

        await _db.ExecuteAsync($@"INSERT INTO {Table} (ID, PROC_INST_ID, NODE_INST_ID, DUE_AT, STATUS, CREATED_AT, FIRED_AT)
VALUES (:Id, :ProcessInstanceId, :NodeInstanceId, :DueAt, :Status, :CreatedAt, :FiredAt)",
            new
            {
                r.Id,
                r.ProcessInstanceId,
                r.NodeInstanceId,
                r.DueAt,
                Status = r.Status.ToString(),
                r.CreatedAt,
                r.FiredAt,
            }, ct);
    }

    /// <summary>Passe la minuterie à Completed ; false si elle n'était plus en attente (déjà traitée ailleurs).</summary>
    public async Task<bool> MarkFiredAsync(long id, DateTime now, CancellationToken ct)
        => await _db.ExecuteAsync($"UPDATE {Table} SET STATUS = :Completed, FIRED_AT = :Now WHERE ID = :Id AND STATUS = :Pending",
            new { Id = id, Now = now, Completed = nameof(WaitStatus.Completed), Pending = nameof(WaitStatus.Pending) }, ct) == 1;

    public Task CancelPendingAsync(long processInstanceId, CancellationToken ct)
        => _db.ExecuteAsync($"UPDATE {Table} SET STATUS = :Cancelled WHERE PROC_INST_ID = :ProcessInstanceId AND STATUS = :Pending",
            new { ProcessInstanceId = processInstanceId, Cancelled = nameof(WaitStatus.Cancelled), Pending = nameof(WaitStatus.Pending) }, ct);
}
