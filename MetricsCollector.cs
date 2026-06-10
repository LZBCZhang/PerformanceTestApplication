using PerformanceTester.Models;

namespace PerformanceTester.Engine;

public static class MetricsCollector
{
    public static EndpointStats Compute(List<RequestResult> results, string? nameOverride = null)
    {
        if (results.Count == 0) throw new ArgumentException("No results to compute.");

        var name = nameOverride ?? results[0].EndpointName;
        var latencies = results
            .Where(r => r.IsSuccess)
            .Select(r => (double)r.ElapsedMs)
            .OrderBy(x => x)
            .ToArray();
    
        var totalElapsedMs = results.Sum(r => r.ElapsedMs);
        var durationSec = totalElapsedMs / 1000.0 / Math.Max(1, config.Concurrency); // wall-clock estimate
        
        // Simpler and more reliable: use actual timestamps with a floor
        var minTs = results.Min(r => r.Timestamp);
        var maxTs = results.Max(r => r.Timestamp);
        var wallClockSec = results[0].WallClockMs / 1000.0;
        
        var rps = wallClockSec > 0
            ? results.Count(r => r.IsSuccess) / wallClockSec
            : results.Count(r => r.IsSuccess);  // fallback: all in <1ms, return count

        return new EndpointStats
        {
            Name = name,
            TotalRequests = results.Count,
            SuccessCount = results.Count(r => r.IsSuccess),
            ErrorCount = results.Count(r => !r.IsSuccess),
            MinMs = latencies.Length > 0 ? latencies[0] : 0,
            MaxMs = latencies.Length > 0 ? latencies[^1] : 0,
            MeanMs = latencies.Length > 0 ? latencies.Average() : 0,
            P50Ms = Percentile(latencies, 50),
            P95Ms = Percentile(latencies, 95),
            P99Ms = Percentile(latencies, 99),
            RPS = rps,
            TotalBytes = results.Sum(r => r.ResponseBytes),
            StatusCodes = results
                .GroupBy(r => r.StatusCode)
                .ToDictionary(g => g.Key, g => g.Count()),
            LatencyHistogram = BuildHistogram(latencies)
        };
    }

    public static double Percentile(double[] sorted, int p)
    {
        if (sorted.Length == 0) return 0;
        var idx = (int)Math.Ceiling(p / 100.0 * sorted.Length) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Length - 1)];
    }

    private static Dictionary<string, int> BuildHistogram(double[] latencies, int bucketMs = 50)
    {
        if (latencies.Length == 0) return [];
        var max = (int)latencies[^1];
        var buckets = new Dictionary<string, int>();
        for (int lo = 0; lo <= max; lo += bucketMs)
        {
            var hi = lo + bucketMs;
            var label = $"{lo}-{hi}ms";
            buckets[label] = latencies.Count(l => l >= lo && l < hi);
        }
        return buckets;
    }
}
