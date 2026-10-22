using Sentier.Configuration;

namespace Sentier.Data.Dialects;

public static class SqlDialects
{
    public static ISqlDialect Create(SentierDatabase database, SentierNames names) => database switch
    {
        SentierDatabase.Oracle => new OracleDialect(names),
        SentierDatabase.Sqlite => new SqliteDialect(names),
        _ => throw new ArgumentOutOfRangeException(nameof(database), database, "Base de données non supportée."),
    };
}
