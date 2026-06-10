using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PerformanceTester.Engine;
using PerformanceTester.Models;
using PerformanceTester.Reports;

// ─── Configuration ────────────────────────────────────────────────────────────
var endpoints = new List<EndpointConfig>
{
    new() { Name = "GET /products",  Url = "https://uat.monapi.com/products",  Method = HttpMethod.Get },
    new() { Name = "GET /users",     Url = "https://uat.monapi.com/users",     Method = HttpMethod.Get },
    new() {
        Name = "POST /orders",
        Url  = "https://uat.monapi.com/orders",
        Method = HttpMethod.Post,
        Body   = """{"productId": 1, "quantity": 2}""",
        Headers = new() { ["Authorization"] = "Bearer <YOUR_TOKEN>" }
    }
};

// Test standard (fixe)
var testConfig = new TestConfig
{
    Endpoints       = endpoints,
    Concurrency     = 20,
    TotalRequests   = 200,
    WarmupRequests  = 10,
};

// Mode ramp-up (1 → 50 en 6 paliers)
var rampUpConfig = new RampUpConfig
{
    StartConcurrency  = 1,
    MaxConcurrency    = 50,
    Steps             = 6,
    RequestsPerStep   = 80,
    StepDelay         = TimeSpan.FromSeconds(2),
    WarmupRequests    = 5,
};

// Seuils de régression
var thresholds = new RegressionThresholds
{
    P95WarningPercent  = 10,
    P95CriticalPercent = 25,
    P99WarningPercent  = 15,
    P99CriticalPercent = 30,
    MeanWarningPercent = 10,
    MeanCriticalPercent = 20,
    ErrorRateWarningAbsolute  = 1,
    ErrorRateCriticalAbsolute = 5,
};

// ─── DI / HttpClient setup ────────────────────────────────────────────────────
var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
    services.AddHttpClient("perf")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                MaxConnectionsPerServer = rampUpConfig.MaxConcurrency * 2,
                AllowAutoRedirect       = true
            })
);

using var host = builder.Build();
var engine = new LoadEngine(host.Services.GetRequiredService<IHttpClientFactory>());

var runLabel   = $"run_{DateTime.UtcNow:yyyyMMdd_HHmm}";
var outputDir  = Directory.CreateDirectory($"reports/{runLabel}").FullName;

// ─── 1. Test standard ─────────────────────────────────────────────────────────
Console.WriteLine("═══════════════════════════════════════");
Console.WriteLine(" PHASE 1 — Test standard (fixe)");
Console.WriteLine("═══════════════════════════════════════");

var allStats = new List<EndpointStats>();
foreach (var ep in endpoints)
{
    int total = testConfig.TotalRequests;
    Console.Write($"  {ep.Name} ...");
    var progress = new Progress<int>(n =>
        Console.Write($"\r  {ep.Name} : {n}/{total}   "));
    var batch = await engine.RunAsync(ep, testConfig, progress);
    allStats.Add(MetricsCollector.Compute(batch.Results, batch.WallClockSec));
    Console.WriteLine(" ✓");
}

ConsoleReportGenerator.Print(allStats);
await ConsoleReportGenerator.SaveJsonAsync(allStats, Path.Combine(outputDir, "stats.json"));

// ─── 2. Ramp-up (sur le premier endpoint) ─────────────────────────────────────
Console.WriteLine("═══════════════════════════════════════");
Console.WriteLine(" PHASE 2 — Ramp-up progressif");
Console.WriteLine("═══════════════════════════════════════");

var rampTarget = endpoints[0];   // ← adapter selon besoin
Console.WriteLine($"  Endpoint cible : {rampTarget.Name}");
Console.WriteLine($"  {rampUpConfig.StartConcurrency} → {rampUpConfig.MaxConcurrency} concurrent(s) en {rampUpConfig.Steps} paliers");
Console.WriteLine();

var rampResults = await engine.RunRampUpAsync(
    rampTarget,
    rampUpConfig,
    onStageStart: (step, total, c) =>
        Console.WriteLine($"  Palier {step}/{total} — concurrence = {c}..."));

ConsoleReportGenerator.PrintRampUp(rampResults);
await ConsoleReportGenerator.SaveJsonAsync(rampResults, Path.Combine(outputDir, "rampup.json"));

// ─── 3. Comparaison avec un run précédent (si baseline.json présent) ──────────
var baselinePath  = "reports/baseline.json";
List<EndpointRegression>? regressions = null;
string? baselineLabel = null;

if (File.Exists(baselinePath))
{
    Console.WriteLine("═══════════════════════════════════════");
    Console.WriteLine(" PHASE 3 — Détection de régressions");
    Console.WriteLine("═══════════════════════════════════════");

    var baselineJson  = await File.ReadAllTextAsync(baselinePath);
    var baselineStats = System.Text.Json.JsonSerializer.Deserialize<List<EndpointStats>>(baselineJson,
        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    if (baselineStats != null)
    {
        baselineLabel = "baseline.json";
        regressions   = RegressionDetector.Compare(baselineStats, allStats, thresholds);
        ConsoleReportGenerator.PrintRegressions(regressions);
        await ConsoleReportGenerator.SaveJsonAsync(regressions, Path.Combine(outputDir, "regressions.json"));
    }
}
else
{
    Console.WriteLine($"  ℹ️  Aucune baseline trouvée dans '{baselinePath}'.");
    Console.WriteLine($"     Copiez le fichier stats.json de votre run de référence vers ce chemin");
    Console.WriteLine($"     pour activer la comparaison lors du prochain run.");
    Console.WriteLine();
    // Sauvegarder automatiquement comme baseline si c'est le premier run
    File.Copy(Path.Combine(outputDir, "stats.json"), baselinePath, overwrite: false);
    Console.WriteLine($"  ✓  Ce run a été sauvegardé comme baseline : {baselinePath}");
}

// ─── 4. Rapport HTML ──────────────────────────────────────────────────────────
var htmlPath = Path.Combine(outputDir, "report.html");
await HtmlReportGenerator.SaveAsync(
    path          : htmlPath,
    runLabel      : runLabel,
    stats         : allStats,
    rampUp        : rampResults,
    regressions   : regressions,
    baselineLabel : baselineLabel);

Console.WriteLine();
Console.WriteLine($"  ✓ Rapport HTML généré : {htmlPath}");
Console.WriteLine($"  ✓ Fichiers JSON dans  : {outputDir}");
Console.WriteLine();
