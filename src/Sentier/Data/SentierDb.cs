using System.Data;
using Dapper;
using Microsoft.Extensions.Options;
using Sentier.Configuration;
using Sentier.Data.Dialects;

namespace Sentier.Data;

/// <summary>Accès Dapper sur la connexion et la transaction injectées par le client.</summary>
internal sealed class SentierDb
{
    private readonly ISentierDbSession _session;
    private readonly SentierOptions _options;

    public SentierDb(ISentierDbSession session, ISqlDialect dialect, SentierNames names, IOptions<SentierOptions> options)
    {
        _session = session;
        _options = options.Value;
        Dialect = dialect;
        Names = names;
    }

    public SentierNames Names { get; }

    public ISqlDialect Dialect { get; }

    public ISentierDbSession Session => _session;

    public Task<int> ExecuteAsync(string sql, object? param, CancellationToken ct)
        => _session.Connection.ExecuteAsync(Command(sql, param, ct));

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param, CancellationToken ct)
        => _session.Connection.QueryFirstOrDefaultAsync<T>(Command(sql, param, ct));

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param, CancellationToken ct)
        => (await _session.Connection.QueryAsync<T>(Command(sql, param, ct))).AsList();

    public Task<long> NextIdAsync(string sequenceName, CancellationToken ct)
        => Dialect.NextValueAsync(_session.Connection, _session.Transaction, sequenceName, ct);

    /// <summary>Valeur de paramètre pour une colonne CLOB (voir <see cref="SentierOptions.LargeTextParameter"/>).</summary>
    public object? LargeText(string? value)
        => _options.LargeTextParameter is { } factory ? factory(value) : value;

    private CommandDefinition Command(string sql, object? param, CancellationToken ct)
        => new(sql, param, _session.Transaction, _options.CommandTimeoutSeconds, CommandType.Text, CommandFlags.Buffered, ct);
}
