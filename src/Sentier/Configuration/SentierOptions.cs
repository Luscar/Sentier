namespace Sentier.Configuration;

/// <summary>Base de données cible du moteur.</summary>
public enum SentierDatabase
{
    Oracle,
    Sqlite,
}

/// <summary>Options du moteur Sentier.</summary>
public sealed class SentierOptions
{
    /// <summary>
    /// Préfixe des tables, vues, séquences, index et contraintes (ex. "AT" donne AT_PROC_INST, AT_SEQ_PROC_INST...).
    /// Permet à plusieurs systèmes de partager le même schéma. 1 à 10 caractères, lettre en premier.
    /// </summary>
    public string TablePrefix { get; set; } = "SEN";

    public SentierDatabase Database { get; set; } = SentierDatabase.Oracle;

    /// <summary>Nombre maximal de noeuds exécutés d'affilée dans une instance (protection contre les boucles infinies).</summary>
    public int MaxStepsPerRun { get; set; } = 1000;

    /// <summary>Profondeur maximale d'imbrication des sous-processus démarrés de façon synchrone.</summary>
    public int MaxSubProcessDepth { get; set; } = 20;

    /// <summary>Délai des commandes SQL en secondes (null = valeur du fournisseur).</summary>
    public int? CommandTimeoutSeconds { get; set; }

    /// <summary>
    /// Fabrique de paramètre pour les colonnes CLOB (variables, contenu des définitions).
    /// Par défaut la chaîne est passée telle quelle. Sous Oracle, pour des contenus &gt; 4000 caractères,
    /// retourner un <c>Dapper.SqlMapper.ICustomQueryParameter</c> qui crée un paramètre <c>OracleDbType.Clob</c>.
    /// </summary>
    public Func<string?, object?>? LargeTextParameter { get; set; }
}
