using System.Data;
using Sentier.Configuration;

namespace Sentier.Data.Dialects;

/// <summary>
/// Différences SQL entre bases supportées. Les requêtes utilisent toutes des paramètres nommés ":Nom",
/// compris à la fois par ODP.NET (Dapper active BindByName) et par Microsoft.Data.Sqlite.
/// </summary>
public interface ISqlDialect
{
    SentierDatabase Database { get; }

    /// <summary>Prochaine valeur de la séquence (clé technique de type long).</summary>
    Task<long> NextValueAsync(IDbConnection connection, IDbTransaction? transaction, string sequenceName, CancellationToken cancellationToken);

    /// <summary>Limite le nombre de lignes d'une requête SELECT (clause ajoutée en fin de requête).</summary>
    string Limit(string selectSql, int count);

    /// <summary>Clause de verrouillage de ligne ajoutée en fin de SELECT (vide si non supporté).</summary>
    string ForUpdateClause { get; }

    /// <summary>Script de création complet (tables, index, vues, séquences), une instruction par élément.</summary>
    IReadOnlyList<string> CreateSchemaScript();

    /// <summary>Script de suppression complet, une instruction par élément.</summary>
    IReadOnlyList<string> DropSchemaScript();
}
