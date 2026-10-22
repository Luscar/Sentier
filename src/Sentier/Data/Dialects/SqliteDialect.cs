using System.Data;
using Dapper;
using Sentier.Configuration;

namespace Sentier.Data.Dialects;

/// <summary>
/// Dialecte SQLite (destiné aux tests). Les séquences Oracle sont émulées par une table
/// {PREFIXE}_SEQUENCE afin de conserver des clés long attribuées avant l'insertion.
/// </summary>
public sealed class SqliteDialect : SqlDialectBase
{
    public SqliteDialect(SentierNames names) : base(names) { }

    public override SentierDatabase Database => SentierDatabase.Sqlite;

    public override string ForUpdateClause => string.Empty;

    protected override string BigIntType => "INTEGER";
    protected override string IntType => "INTEGER";
    protected override string ClobType => "TEXT";
    protected override string TimestampType => "TEXT";
    protected override string StringType(int length) => "TEXT";

    public override async Task<long> NextValueAsync(IDbConnection connection, IDbTransaction? transaction, string sequenceName, CancellationToken cancellationToken)
    {
        var table = Names.SequenceTable;
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            $"UPDATE {table} SET LAST_VALUE = LAST_VALUE + 1 WHERE SEQ_NAME = :Name",
            new { Name = sequenceName }, transaction, cancellationToken: cancellationToken));

        if (updated != 1)
        {
            throw new SentierConfigurationException($"Séquence '{sequenceName}' absente de la table {table}.");
        }

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT LAST_VALUE FROM {table} WHERE SEQ_NAME = :Name",
            new { Name = sequenceName }, transaction, cancellationToken: cancellationToken));
    }

    public override string Limit(string selectSql, int count) => $"{selectSql} LIMIT {count}";

    protected override IEnumerable<string> CreateSequences()
    {
        yield return $"CREATE TABLE {Names.SequenceTable} (SEQ_NAME TEXT NOT NULL PRIMARY KEY, LAST_VALUE INTEGER NOT NULL)";
        foreach (var sequence in Names.Sequences)
        {
            yield return $"INSERT INTO {Names.SequenceTable} (SEQ_NAME, LAST_VALUE) VALUES ('{sequence}', 0)";
        }
    }

    protected override IEnumerable<string> DropSequences()
    {
        yield return $"DROP TABLE IF EXISTS {Names.SequenceTable}";
    }

    protected override string DropTable(string table) => $"DROP TABLE IF EXISTS {table}";

    protected override string DropView(string view) => $"DROP VIEW IF EXISTS {view}";
}
