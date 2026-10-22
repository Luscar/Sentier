namespace Sentier.Configuration;

/// <summary>Horloge du moteur (remplaçable pour les tests). Toutes les dates sont en UTC.</summary>
public interface ISentierClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemSentierClock : ISentierClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
