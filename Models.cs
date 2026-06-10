namespace PerformanceTester.Models;

public record TestConfig
{
    public required List<EndpointConfig> Endpoints { get; init; }
    public int Concurrency { get; init; } = 10;
    public int TotalRequests { get; init; } = 100;
    public int WarmupRequests { get; init; } = 5;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

public record EndpointConfig
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public HttpMethod Method { get; init; } = HttpMethod.Get;
    public Dictionary<string, string>? Headers { get; init; }
    public string? Body { get; init; }
    public string? ContentType { get; init; }
}

public record RequestResult
{
    public required string EndpointName { get; init; }
    public int StatusCode { get; init; }
    public long ElapsedMs { get; init; }
    public long ResponseBytes { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; }
    public long WallClockMs { get; init; } 
}

public record EndpointStats
{
    public required string Name { get; init; }
    public int TotalRequests { get; init; }
    public int SuccessCount { get; init; }
    public int ErrorCount { get; init; }
    public double ErrorRate => TotalRequests == 0 ? 0 : (double)ErrorCount / TotalRequests * 100;
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double MeanMs { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
    public double RPS { get; init; }
    public long TotalBytes { get; init; }
    public Dictionary<int, int> StatusCodes { get; init; } = [];
    // Histogramme des latences (buckets de 50ms)
    public Dictionary<string, int> LatencyHistogram { get; init; } = [];
}
