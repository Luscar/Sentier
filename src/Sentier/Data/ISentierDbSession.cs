using System.Data;

namespace Sentier.Data;

/// <summary>
/// Connexion et transaction fournies par le client. Le moteur n'ouvre, ne valide (commit)
/// et n'annule (rollback) jamais rien : toutes les écritures se font dans la transaction du client.
/// </summary>
public interface ISentierDbSession
{
    IDbConnection Connection { get; }

    IDbTransaction? Transaction { get; }
}

/// <summary>Implémentation simple d'une session à partir d'une connexion et d'une transaction existantes.</summary>
public sealed class SentierDbSession : ISentierDbSession
{
    public SentierDbSession(IDbConnection connection, IDbTransaction? transaction = null)
    {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        Transaction = transaction;
    }

    public IDbConnection Connection { get; }

    public IDbTransaction? Transaction { get; }
}

/// <summary>Session résolue paresseusement (utile quand la transaction est démarrée après la création du scope).</summary>
internal sealed class DelegateSentierDbSession : ISentierDbSession
{
    private readonly Func<IDbConnection> _connection;
    private readonly Func<IDbTransaction?> _transaction;

    public DelegateSentierDbSession(Func<IDbConnection> connection, Func<IDbTransaction?> transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    public IDbConnection Connection => _connection();

    public IDbTransaction? Transaction => _transaction();
}
