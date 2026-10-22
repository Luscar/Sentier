using Sentier.Data.Records;

namespace Sentier.Data.Stores;

internal sealed class SignalSubscriptionStore
{
    private readonly SentierDb _db;

    public SignalSubscriptionStore(SentierDb db) => _db = db;

    private string Table => _db.Names.SignalSubscription;

    private string SelectSql => $@"SELECT ID AS Id, PROC_INST_ID AS ProcessInstanceId, NODE_INST_ID AS NodeInstanceId, SIGNAL_NAME AS SignalName,
       CORRELATION_KEY AS CorrelationKey, STATUS AS Status, CREATED_AT AS CreatedAt, RECEIVED_AT AS ReceivedAt
FROM {Table}";

    /// <param name="correlationKey">null : tous les abonnés au signal (diffusion) ; sinon uniquement ceux de cette clé.</param>
    public Task<IReadOnlyList<SignalSubscriptionRecord>> FindPendingAsync(string signalName, string? correlationKey, CancellationToken ct)
    {
        var sql = $"{SelectSql} WHERE SIGNAL_NAME = :SignalName AND STATUS = :Pending";
        if (correlationKey is not null)
        {
            sql += " AND CORRELATION_KEY = :CorrelationKey";
        }

        return _db.QueryAsync<SignalSubscriptionRecord>(sql + " ORDER BY ID",
            new { SignalName = signalName, CorrelationKey = correlationKey, Pending = nameof(WaitStatus.Pending) }, ct);
    }

    public async Task InsertAsync(SignalSubscriptionRecord r, CancellationToken ct)
    {
        r.Id = await _db.NextIdAsync(_db.Names.SignalSubscriptionSequence, ct);

        await _db.ExecuteAsync($@"INSERT INTO {Table} (ID, PROC_INST_ID, NODE_INST_ID, SIGNAL_NAME, CORRELATION_KEY, STATUS, CREATED_AT, RECEIVED_AT)
VALUES (:Id, :ProcessInstanceId, :NodeInstanceId, :SignalName, :CorrelationKey, :Status, :CreatedAt, :ReceivedAt)",
            new
            {
                r.Id,
                r.ProcessInstanceId,
                r.NodeInstanceId,
                r.SignalName,
                r.CorrelationKey,
                Status = r.Status.ToString(),
                r.CreatedAt,
                r.ReceivedAt,
            }, ct);
    }

    public async Task<bool> MarkReceivedAsync(long id, DateTime now, CancellationToken ct)
        => await _db.ExecuteAsync($"UPDATE {Table} SET STATUS = :Completed, RECEIVED_AT = :Now WHERE ID = :Id AND STATUS = :Pending",
            new { Id = id, Now = now, Completed = nameof(WaitStatus.Completed), Pending = nameof(WaitStatus.Pending) }, ct) == 1;

    public Task CancelPendingAsync(long processInstanceId, CancellationToken ct)
        => _db.ExecuteAsync($"UPDATE {Table} SET STATUS = :Cancelled WHERE PROC_INST_ID = :ProcessInstanceId AND STATUS = :Pending",
            new { ProcessInstanceId = processInstanceId, Cancelled = nameof(WaitStatus.Cancelled), Pending = nameof(WaitStatus.Pending) }, ct);
}
