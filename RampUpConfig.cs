namespace PerformanceTester.Models;

/// <summary>
/// Configuration d'un test en mode ramp-up progressif.
/// </summary>
public record RampUpConfig
{
    /// <summary>Concurrence de départ.</summary>
    public int StartConcurrency { get; init; } = 1;

    /// <summary>Concurrence maximale.</summary>
    public int MaxConcurrency { get; init; } = 50;

    /// <summary>Nombre de paliers entre Start et Max.</summary>
    public int Steps { get; init; } = 5;

    /// <summary>Nombre de requêtes envoyées à chaque palier.</summary>
    public int RequestsPerStep { get; init; } = 50;

    /// <summary>Pause entre deux paliers (laisse l'API se stabiliser).</summary>
    public TimeSpan StepDelay { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Requêtes de warmup avant le premier palier.</summary>
    public int WarmupRequests { get; init; } = 5;

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Calcule les niveaux de concurrence de chaque palier.</summary>
    public IEnumerable<int> GetConcurrencyLevels()
    {
        if (Steps <= 1) { yield return MaxConcurrency; yield break; }
        for (int i = 0; i < Steps; i++)
        {
            int c = StartConcurrency + (int)Math.Round(
                (double)(MaxConcurrency - StartConcurrency) / (Steps - 1) * i);
            yield return Math.Clamp(c, StartConcurrency, MaxConcurrency);
        }
    }
}

/// <summary>Résultats d'un palier de ramp-up.</summary>
public record RampUpStageResult
{
    public required int Concurrency { get; init; }
    public required EndpointStats Stats { get; init; }
}
