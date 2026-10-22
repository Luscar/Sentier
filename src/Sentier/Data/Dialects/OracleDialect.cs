using System.Data;
using Dapper;
using Sentier.Configuration;

namespace Sentier.Data.Dialects;

/// <summary>Dialecte Oracle (12c+ pour FETCH FIRST). Les clés proviennent de séquences Oracle.</summary>
public sealed class OracleDialect : SqlDialectBase
{
    public OracleDialect(SentierNames names) : base(names) { }

    public override SentierDatabase Database => SentierDatabase.Oracle;

    public override string ForUpdateClause => " FOR UPDATE";

    protected override string BigIntType => "NUMBER(19)";
    protected override string IntType => "NUMBER(10)";
    protected override string ClobType => "CLOB";
    protected override string TimestampType => "TIMESTAMP";
    protected override string StringType(int length) => $"VARCHAR2({length} CHAR)";

    public override Task<long> NextValueAsync(IDbConnection connection, IDbTransaction? transaction, string sequenceName, CancellationToken cancellationToken)
        => connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT {sequenceName}.NEXTVAL FROM DUAL", transaction: transaction, cancellationToken: cancellationToken));

    public override string Limit(string selectSql, int count) => $"{selectSql} FETCH FIRST {count} ROWS ONLY";

    protected override IEnumerable<string> CreateSequences()
        => Names.Sequences.Select(s => $"CREATE SEQUENCE {s} START WITH 1 INCREMENT BY 1 NOCYCLE");

    protected override IEnumerable<string> DropSequences()
        => Names.Sequences.Select(s => $"DROP SEQUENCE {s}");

    protected override string DropTable(string table) => $"DROP TABLE {table} CASCADE CONSTRAINTS PURGE";

    protected override string DropView(string view) => $"DROP VIEW {view}";
}
