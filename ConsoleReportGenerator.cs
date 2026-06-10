using System.Text.Json;
using PerformanceTester.Models;

namespace PerformanceTester.Reports;

public static class ConsoleReportGenerator
{
    public static void Print(List<EndpointStats> stats)
    {
        Console.WriteLine();
        Console.WriteLine("╔══ PERFORMANCE REPORT ══════════════════════════════════════╗");
        foreach (var s in stats)
        {
            Console.WriteLine($"\n  ▶ {s.Name}");
            Console.WriteLine($"    Requêtes   : {s.TotalRequests} total | {s.SuccessCount} OK | {s.ErrorCount} erreurs ({s.ErrorRate:F1}%)");
            Console.WriteLine($"    Throughput : {s.RPS:F1} req/s");
            Console.WriteLine($"    Latences   : min={s.MinMs:F0}ms  mean={s.MeanMs:F0}ms  max={s.MaxMs:F0}ms");
            Console.WriteLine($"    Percentiles: P50={s.P50Ms:F0}ms  P95={s.P95Ms:F0}ms  P99={s.P99Ms:F0}ms");
            var codes = string.Join("  ", s.StatusCodes.Select(kv => $"HTTP {kv.Key}: {kv.Value}"));
            Console.WriteLine($"    Codes HTTP : {codes}");
        }
        Console.WriteLine("\n╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    public static void PrintRampUp(List<RampUpStageResult> stages)
    {
        Console.WriteLine();
        Console.WriteLine("╔══ RAMP-UP RESULTS ════════════════════════════════════════╗");
        Console.WriteLine($"  {"Concurrence",-14} {"RPS",8} {"Mean",8} {"P95",8} {"P99",8} {"Erreurs",10}");
        Console.WriteLine($"  {"──────────────",-14} {"────────",8} {"────────",8} {"────────",8} {"────────",8} {"──────────",10}");
        foreach (var stage in stages)
        {
            var s = stage.Stats;
            Console.WriteLine($"  {stage.Concurrency,-14} {s.RPS,8:F1} {s.MeanMs,8:F0} {s.P95Ms,8:F0} {s.P99Ms,8:F0} {s.ErrorRate,9:F1}%");
        }
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    public static void PrintRegressions(List<EndpointRegression> regressions)
    {
        Console.WriteLine();
        Console.WriteLine("╔══ REGRESSION REPORT ═══════════════════════════════════════╗");
        foreach (var ep in regressions)
        {
            var icon = ep.OverallSeverity == RegressionSeverity.Critical ? "✗ CRITICAL"
                     : ep.OverallSeverity == RegressionSeverity.Warning ? "⚠ WARNING"
                     : "✓ OK";
            Console.WriteLine($"\n  {icon}  {ep.EndpointName}");
            foreach (var d in ep.Diffs)
            {
                var sign = d.DeltaPercent >= 0 ? "+" : "";
                var arrow = d.IsRegression ? "↑" : "↓";
                Console.WriteLine($"    {d.Metric,-12} {d.Baseline,8:F1} → {d.Current,8:F1} {d.Unit}  {arrow} {sign}{d.DeltaPercent:F1}%  [{d.Severity}]");
            }
        }
        Console.WriteLine("\n╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    public static async Task SaveJsonAsync(object data, string path)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }
}
