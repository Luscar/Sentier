using System.Data;
using Dapper;
using Sentier.Configuration;
using Sentier.Data.Dialects;

namespace Sentier.Data;

/// <summary>
/// Génération et exécution du schéma Sentier pour un préfixe donné.
/// Sous Oracle le DDL est auto-validé : préférer fournir le script au DBA via <see cref="GetCreateScript"/>.
/// </summary>
public static class SentierSchema
{
    public static IReadOnlyList<string> GetCreateStatements(SentierDatabase database, string tablePrefix)
        => SqlDialects.Create(database, new SentierNames(tablePrefix)).CreateSchemaScript();

    public static IReadOnlyList<string> GetDropStatements(SentierDatabase database, string tablePrefix)
        => SqlDialects.Create(database, new SentierNames(tablePrefix)).DropSchemaScript();

    /// <summary>Script texte prêt à exécuter (chaque instruction terminée par ";").</summary>
    public static string GetCreateScript(SentierDatabase database, string tablePrefix)
        => ToScript(GetCreateStatements(database, tablePrefix));

    public static string GetDropScript(SentierDatabase database, string tablePrefix)
        => ToScript(GetDropStatements(database, tablePrefix));

    public static Task CreateAsync(IDbConnection connection, SentierDatabase database, string tablePrefix, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(connection, GetCreateStatements(database, tablePrefix), transaction, cancellationToken);

    public static Task DropAsync(IDbConnection connection, SentierDatabase database, string tablePrefix, IDbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(connection, GetDropStatements(database, tablePrefix), transaction, cancellationToken);

    private static async Task ExecuteAsync(IDbConnection connection, IEnumerable<string> statements, IDbTransaction? transaction, CancellationToken cancellationToken)
    {
        foreach (var statement in statements)
        {
            await connection.ExecuteAsync(new CommandDefinition(statement, transaction: transaction, cancellationToken: cancellationToken));
        }
    }

    private static string ToScript(IEnumerable<string> statements)
        => string.Join(Environment.NewLine + Environment.NewLine, statements.Select(s => s + ";")) + Environment.NewLine;
}
