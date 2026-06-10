using PerformanceTester.Models;

namespace PerformanceTester.Engine;

public enum RegressionSeverity { Ok, Warning, Critical }

public record MetricDiff
{
    public required string Metric { get; init; }
    public required double Baseline { get; init; }
    public required double Current { get; init; }
    public double DeltaPercent => Baseline == 0 ? 0 : (Current - Baseline) / Baseline * 100;
    public bool IsRegression { get; init; }
    public required RegressionSeverity Severity { get; init; }
    public string Unit { get; init; } = "ms";
}

public record EndpointRegression
{
    public required string EndpointName { get; init; }
    public required List<MetricDiff> Diffs { get; init; }
    public RegressionSeverity OverallSeverity =>
        Diffs.Any(d => d.Severity == RegressionSeverity.Critical) ? RegressionSeverity.Critical :
        Diffs.Any(d => d.Severity == RegressionSeverity.Warning) ? RegressionSeverity.Warning :
        RegressionSeverity.Ok;
}

/// <summary>
/// Seuils de régression (configurable).
/// </summary>
public record RegressionThresholds
{
    /// <summary>% de dégradation P95 déclenchant un Warning.</summary>
    public double P95WarningPercent { get; init; } = 10;
    /// <summary>% de dégradation P95 déclenchant un Critical.</summary>
    public double P95CriticalPercent { get; init; } = 25;

    public double P99WarningPercent { get; init; } = 15;
    public double P99CriticalPercent { get; init; } = 30;

    public double MeanWarningPercent { get; init; } = 10;
    public double MeanCriticalPercent { get; init; } = 20;

    public double ErrorRateWarningAbsolute { get; init; } = 1;   // points de %
    public double ErrorRateCriticalAbsolute { get; init; } = 5;

    public double RPSWarningPercent { get; init; } = -10;   // baisse de RPS
    public double RPSCriticalPercent { get; init; } = -25;
}

public static class RegressionDetector
{
    public static List<EndpointRegression> Compare(
        List<EndpointStats> baseline,
        List<EndpointStats> current,
        RegressionThresholds? thresholds = null)
    {
        thresholds ??= new RegressionThresholds();
        var results = new List<EndpointRegression>();

        foreach (var baseEp in baseline)
        {
            var curEp = current.FirstOrDefault(e => e.Name == baseEp.Name);
            if (curEp is null) continue;

            var diffs = new List<MetricDiff>
            {
                CompareLatency("P50", baseEp.P50Ms, curEp.P50Ms,
                    thresholds.MeanWarningPercent, thresholds.MeanCriticalPercent),
                CompareLatency("Mean", baseEp.MeanMs, curEp.MeanMs,
                    thresholds.MeanWarningPercent, thresholds.MeanCriticalPercent),
                CompareLatency("P95", baseEp.P95Ms, curEp.P95Ms,
                    thresholds.P95WarningPercent, thresholds.P95CriticalPercent),
                CompareLatency("P99", baseEp.P99Ms, curEp.P99Ms,
                    thresholds.P99WarningPercent, thresholds.P99CriticalPercent),
                CompareErrorRate(baseEp.ErrorRate, curEp.ErrorRate,
                    thresholds.ErrorRateWarningAbsolute, thresholds.ErrorRateCriticalAbsolute),
                CompareRPS(baseEp.RPS, curEp.RPS,
                    thresholds.RPSWarningPercent, thresholds.RPSCriticalPercent),
            };

            results.Add(new EndpointRegression
            {
                EndpointName = baseEp.Name,
                Diffs = diffs
            });
        }

        return results;
    }

    private static MetricDiff CompareLatency(
        string metric, double baseline, double current,
        double warnPct, double critPct)
    {
        var delta = baseline == 0 ? 0 : (current - baseline) / baseline * 100;
        return new MetricDiff
        {
            Metric = metric,
            Baseline = Math.Round(baseline, 1),
            Current = Math.Round(current, 1),
            IsRegression = delta > warnPct,
            Severity = delta >= critPct ? RegressionSeverity.Critical
                     : delta >= warnPct ? RegressionSeverity.Warning
                     : RegressionSeverity.Ok,
            Unit = "ms"
        };
    }

    private static MetricDiff CompareErrorRate(
        double baseline, double current, double warnAbs, double critAbs)
    {
        var delta = current - baseline;
        return new MetricDiff
        {
            Metric = "Error rate",
            Baseline = Math.Round(baseline, 2),
            Current = Math.Round(current, 2),
            IsRegression = delta > warnAbs,
            Severity = delta >= critAbs ? RegressionSeverity.Critical
                     : delta >= warnAbs ? RegressionSeverity.Warning
                     : RegressionSeverity.Ok,
            Unit = "%"
        };
    }

    private static MetricDiff CompareRPS(
        double baseline, double current, double warnPct, double critPct)
    {
        var delta = baseline == 0 ? 0 : (current - baseline) / baseline * 100;
        // Pour le RPS, une baisse = régression (seuils négatifs)
        return new MetricDiff
        {
            Metric = "RPS",
            Baseline = Math.Round(baseline, 1),
            Current = Math.Round(current, 1),
            IsRegression = delta < warnPct,
            Severity = delta <= critPct ? RegressionSeverity.Critical
                     : delta <= warnPct ? RegressionSeverity.Warning
                     : RegressionSeverity.Ok,
            Unit = "req/s"
        };
    }
}
