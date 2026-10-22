using Sentier.Data.Records;

namespace Sentier.Data.Stores;

internal sealed class UserTaskStore
{
    private readonly SentierDb _db;

    public UserTaskStore(SentierDb db) => _db = db;

    private string Table => _db.Names.UserTask;

    private string SelectSql => $@"SELECT ID AS Id, PROC_INST_ID AS ProcessInstanceId, NODE_INST_ID AS NodeInstanceId, TASK_CODE AS TaskCode,
       EXTERNAL_ID AS ExternalId, STATUS AS Status, CREATED_AT AS CreatedAt, COMPLETED_AT AS CompletedAt,
       COMPLETED_BY AS CompletedBy, OUTCOME AS Outcome
FROM {Table}";

    public Task<UserTaskRecord?> GetAsync(long id, CancellationToken ct)
        => _db.QueryFirstOrDefaultAsync<UserTaskRecord>($"{SelectSql} WHERE ID = :Id", new { Id = id }, ct);

    public Task<UserTaskRecord?> GetByExternalIdAsync(string externalId, CancellationToken ct)
        => _db.QueryFirstOrDefaultAsync<UserTaskRecord>($"{SelectSql} WHERE EXTERNAL_ID = :ExternalId ORDER BY ID DESC", new { ExternalId = externalId }, ct);

    public Task<IReadOnlyList<UserTaskRecord>> GetPendingForInstanceAsync(long processInstanceId, CancellationToken ct)
        => _db.QueryAsync<UserTaskRecord>($"{SelectSql} WHERE PROC_INST_ID = :ProcessInstanceId AND STATUS = :Pending ORDER BY ID",
            new { ProcessInstanceId = processInstanceId, Pending = nameof(WaitStatus.Pending) }, ct);

    public async Task InsertAsync(UserTaskRecord r, CancellationToken ct)
    {
        r.Id = await _db.NextIdAsync(_db.Names.UserTaskSequence, ct);

        await _db.ExecuteAsync($@"INSERT INTO {Table} (ID, PROC_INST_ID, NODE_INST_ID, TASK_CODE, EXTERNAL_ID, STATUS, CREATED_AT, COMPLETED_AT, COMPLETED_BY, OUTCOME)
VALUES (:Id, :ProcessInstanceId, :NodeInstanceId, :TaskCode, :ExternalId, :Status, :CreatedAt, :CompletedAt, :CompletedBy, :Outcome)",
            new
            {
                r.Id,
                r.ProcessInstanceId,
                r.NodeInstanceId,
                r.TaskCode,
                r.ExternalId,
                Status = r.Status.ToString(),
                r.CreatedAt,
                r.CompletedAt,
                r.CompletedBy,
                r.Outcome,
            }, ct);
    }

    public Task SetExternalIdAsync(long id, string externalId, CancellationToken ct)
        => _db.ExecuteAsync($"UPDATE {Table} SET EXTERNAL_ID = :ExternalId WHERE ID = :Id", new { Id = id, ExternalId = externalId }, ct);

    public async Task<bool> CompleteAsync(long id, string? completedBy, string? outcome, DateTime now, CancellationToken ct)
        => await _db.ExecuteAsync($@"UPDATE {Table} SET STATUS = :Completed, COMPLETED_AT = :Now, COMPLETED_BY = :CompletedBy, OUTCOME = :Outcome
WHERE ID = :Id AND STATUS = :Pending",
            new
            {
                Id = id,
                Now = now,
                CompletedBy = completedBy,
                Outcome = outcome,
                Completed = nameof(WaitStatus.Completed),
                Pending = nameof(WaitStatus.Pending),
            }, ct) == 1;

    public Task CancelAsync(long id, DateTime now, CancellationToken ct)
        => _db.ExecuteAsync($"UPDATE {Table} SET STATUS = :Cancelled, COMPLETED_AT = :Now WHERE ID = :Id AND STATUS = :Pending",
            new { Id = id, Now = now, Cancelled = nameof(WaitStatus.Cancelled), Pending = nameof(WaitStatus.Pending) }, ct);
}
