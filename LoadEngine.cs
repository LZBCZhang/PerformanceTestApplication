using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using PerformanceTester.Models;

namespace PerformanceTester.Engine;

public class LoadEngine(IHttpClientFactory httpClientFactory)
{
    public async Task<BatchResult> RunAsync(
        EndpointConfig endpoint,
        TestConfig config,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("perf");
    
        // Warmup — result discarded
        await RunBatchAsync(endpoint, config.WarmupRequests, 1, client, config.Timeout, null, ct);
    
        return await RunBatchAsync(
            endpoint, config.TotalRequests, config.Concurrency,
            client, config.Timeout, progress, ct);
    }

    // ─── Mode ramp-up ─────────────────────────────────────────────────────────
    public async Task<List<RampUpStageResult>> RunRampUpAsync(
        EndpointConfig endpoint,
        RampUpConfig rampUp,
        Action<int, int, int>? onStageStart = null,   // (stepIndex, totalSteps, concurrency)
        CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("perf");
        var stageResults = new List<RampUpStageResult>();

        var levels = rampUp.GetConcurrencyLevels().ToList();

        // Warmup avant le premier palier
        await RunBatchAsync(endpoint, rampUp.WarmupRequests, 1, client, rampUp.Timeout, null, ct);

        for (int i = 0; i < levels.Count; i++)
        {
            var concurrency = levels[i];
            onStageStart?.Invoke(i + 1, levels.Count, concurrency);

            var results = await RunBatchAsync(
                endpoint, rampUp.RequestsPerStep, concurrency,
                client, rampUp.Timeout, null, ct);

            var stats = MetricsCollector.Compute(results, $"concurrency={concurrency}");
            stageResults.Add(new RampUpStageResult { Concurrency = concurrency, Stats = stats });

            if (i < levels.Count - 1 && rampUp.StepDelay > TimeSpan.Zero)
                await Task.Delay(rampUp.StepDelay, ct);
        }

        return stageResults;
    }

    // ─── Noyau commun ─────────────────────────────────────────────────────────
    private async Task<BatchResult> RunBatchAsync(
        EndpointConfig endpoint,
        int count,
        int concurrency,
        HttpClient client,
        TimeSpan timeout,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        if (count <= 0) return new BatchResult { Results = [], WallClockSec = 0 };
    
        var results = new ConcurrentBag<RequestResult>();
        var semaphore = new SemaphoreSlim(concurrency);
        int completed = 0;
    
        var wallClock = Stopwatch.StartNew();  // ← start before first request
    
        var tasks = Enumerable.Range(0, count).Select(async _ =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var result = await SendRequestAsync(endpoint, client, timeout, ct);
                results.Add(result);
                progress?.Report(Interlocked.Increment(ref completed));
            }
            finally { semaphore.Release(); }
        });
    
        await Task.WhenAll(tasks);
        wallClock.Stop();  // ← stop after last request
    
        return new BatchResult
        {
            Results = [.. results],
            WallClockSec = Math.Max(wallClock.Elapsed.TotalSeconds, 0.001) // never zero
        };
    }

    private async Task<RequestResult> SendRequestAsync(
        EndpointConfig endpoint, HttpClient client,
        TimeSpan timeout, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var request = BuildRequest(endpoint);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);

            var response = await client.SendAsync(request, cts.Token);
            var body = await response.Content.ReadAsByteArrayAsync(ct);
            sw.Stop();

            return new RequestResult
            {
                EndpointName = endpoint.Name,
                StatusCode = (int)response.StatusCode,
                ElapsedMs = sw.ElapsedMilliseconds,
                ResponseBytes = body.Length,
                IsSuccess = response.IsSuccessStatusCode,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new RequestResult
            {
                EndpointName = endpoint.Name,
                StatusCode = 0,
                ElapsedMs = sw.ElapsedMilliseconds,
                ResponseBytes = 0,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    private static HttpRequestMessage BuildRequest(EndpointConfig endpoint)
    {
        var request = new HttpRequestMessage(endpoint.Method, endpoint.Url);
        if (endpoint.Headers != null)
            foreach (var (k, v) in endpoint.Headers)
                request.Headers.TryAddWithoutValidation(k, v);
        if (endpoint.Body != null)
            request.Content = new StringContent(
                endpoint.Body, Encoding.UTF8,
                endpoint.ContentType ?? "application/json");
        return request;
    }
}
