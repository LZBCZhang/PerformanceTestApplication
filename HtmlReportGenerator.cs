using System.Text;
using System.Text.Json;
using PerformanceTester.Models;

namespace PerformanceTester.Reports;

public static class HtmlReportGenerator
{
    public static async Task SaveAsync(
        string path,
        string runLabel,
        List<EndpointStats> stats,
        List<RampUpStageResult>? rampUp = null,
        List<EndpointRegression>? regressions = null,
        string? baselineLabel = null)
    {
        var html = Build(runLabel, stats, rampUp, regressions, baselineLabel);
        await File.WriteAllTextAsync(path, html, Encoding.UTF8);
    }

    private static string Build(
        string runLabel,
        List<EndpointStats> stats,
        List<RampUpStageResult>? rampUp,
        List<EndpointRegression>? regressions,
        string? baselineLabel)
    {
        var statsJson = JsonSerializer.Serialize(stats);
        var rampJson  = rampUp       != null ? JsonSerializer.Serialize(rampUp)       : "null";
        var regJson   = regressions  != null ? JsonSerializer.Serialize(regressions)  : "null";

        return $$"""
<!DOCTYPE html>
<html lang="fr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Perf Report — {{runLabel}}</title>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
<style>
  :root {
    --bg: #f4f6f9;
    --surface: #ffffff;
    --card: #3d4459;
    --border: #dde1ea;
    --text: #1e2433;
    --muted: #8891a8;
    --accent: #4f46e5;
    --green: #16a34a;
    --yellow: #d97706;
    --red: #dc2626;
    --font: 'SF Mono', 'Fira Code', 'Consolas', monospace;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { background: var(--bg); color: var(--text); font-family: var(--font); font-size: 13px; line-height: 1.6; }

  header { background: var(--surface); border-bottom: 1px solid var(--border); padding: 16px 28px; display: flex; align-items: center; gap: 12px; }
  .hdr-icon { width: 28px; height: 28px; background: #eef0ff; border-radius: 6px; display: flex; align-items: center; justify-content: center; font-size: 14px; }
  header h1 { font-size: 15px; font-weight: 600; color: var(--text); }
  header .meta { font-size: 11px; color: var(--muted); }
  header .meta-date { font-size: 11px; color: var(--muted); margin-left: auto; }

  nav { background: var(--surface); border-bottom: 1px solid var(--border); padding: 0 28px; display: flex; gap: 0; }
  nav button { background: none; border: none; border-bottom: 2px solid transparent; color: var(--muted);
    font: inherit; font-size: 12px; padding: 10px 16px; cursor: pointer; margin-bottom: -1px; }
  nav button.active { color: var(--accent); border-bottom-color: var(--accent); font-weight: 600; }

  main { padding: 24px 28px; max-width: 1200px; }
  .section { display: none; } .section.active { display: block; }
  h2 { font-size: 11px; font-weight: 600; color: var(--muted); text-transform: uppercase; letter-spacing: 0.8px; margin-bottom: 16px; }
  h3 { font-size: 12px; font-weight: 600; color: var(--accent); margin: 24px 0 10px; }

  .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 10px; margin-bottom: 20px; }
  .kpi { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; padding: 12px 14px; }
  .kpi .label { font-size: 10px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.7px; margin-bottom: 5px; }
  .kpi .value { font-size: 20px; font-weight: 700; color: var(--text); }
  .kpi .unit  { font-size: 10px; color: var(--muted); margin-left: 2px; }

  .tbl-wrap { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; overflow: hidden; }
  table { width: 100%; border-collapse: collapse; font-size: 12px; }
  th { background: #f8f9fb; text-align: left; color: var(--muted); font-weight: 600; font-size: 10px;
    text-transform: uppercase; letter-spacing: 0.6px; padding: 9px 14px; border-bottom: 1px solid var(--border); }
  td { padding: 10px 14px; border-bottom: 1px solid #f0f2f6; }
  tr:last-child td { border-bottom: none; }
  tr:hover td { background: #f8f9fb; }
  .ep-name { color: var(--accent); font-weight: 600; }

  .badge { display: inline-block; padding: 2px 7px; border-radius: 4px; font-size: 10px; font-weight: 700; }
  .ok   { background: #dcfce7; color: #166534; }
  .warn { background: #fef3c7; color: #92400e; }
  .crit { background: #fee2e2; color: #991b1b; }

  .chart-wrap { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; padding: 16px; margin-bottom: 16px; }
  .chart-wrap canvas { max-height: 280px; }
  .charts-2col { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }

  .reg-card { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; padding: 16px; margin-bottom: 12px; }
  .reg-header { display: flex; align-items: center; gap: 8px; margin-bottom: 14px; }
  .reg-ep { font-weight: 600; color: var(--text); font-size: 13px; }
  .diff-row { display: grid; grid-template-columns: 70px 1fr 70px 100px 80px; gap: 8px;
    align-items: center; padding: 5px 0; border-bottom: 1px solid #f0f2f6; font-size: 11px; }
  .diff-row:last-child { border-bottom: none; }
  .diff-metric { color: var(--muted); }
  .diff-bar-wrap { background: #f0f2f6; border-radius: 3px; height: 5px; }
  .diff-bar { height: 5px; border-radius: 3px; }
  .diff-delta { text-align: right; }
  .delta-pos { color: var(--red); }
  .delta-neg { color: var(--green); }
  .diff-vals { color: var(--muted); font-size: 10px; }

  .ramp-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(110px, 1fr)); gap: 10px; margin-bottom: 16px; }
  .ramp-step { background: var(--card); border: 1px solid #555c73; border-radius: 8px;
    padding: 12px; text-align: center; cursor: pointer; transition: border-color .15s; }
  .ramp-step:hover, .ramp-step.selected { border-color: #818cf8; }
  .ramp-step .conc { font-size: 20px; font-weight: 700; color: #a5b4fc; }
  .ramp-step .sub  { font-size: 10px; color: #9ca3af; margin-top: 2px; }
  .ramp-step .rps  { font-size: 13px; font-weight: 600; color: #f1f5f9; margin-top: 4px; }

  @media (max-width: 700px) {
    header, nav, main { padding-left: 16px; padding-right: 16px; }
    .charts-2col { grid-template-columns: 1fr; }
  }
</style>
</head>
<body>

<header>
  <div class="hdr-icon">⚡</div>
  <h1>Performance Report <span class="meta">{{runLabel}}</span></h1>
  <span class="meta-date" id="run-date"></span>
</header>

<nav>
  <button class="active" onclick="showTab('overview', this)">Vue d'ensemble</button>
  <button onclick="showTab('latency', this)">Latences</button>
  <button id="tab-rampup" onclick="showTab('rampup', this)" style="display:none">Ramp-up</button>
  <button id="tab-regression" onclick="showTab('regression', this)" style="display:none">Régressions</button>
</nav>

<main>

<div class="section active" id="sec-overview">
  <h2>Résumé global</h2>
  <div class="kpi-grid" id="global-kpis"></div>
  <div class="tbl-wrap">
    <table>
      <thead>
        <tr>
          <th>Endpoint</th><th>Requêtes</th><th>Erreurs</th><th>RPS</th>
          <th>Mean</th><th>P50</th><th>P95</th><th>P99</th><th>Max</th>
        </tr>
      </thead>
      <tbody id="overview-tbody"></tbody>
    </table>
  </div>
</div>

<div class="section" id="sec-latency">
  <h2>Latences par endpoint</h2>
  <div id="latency-charts"></div>
</div>

<div class="section" id="sec-rampup">
  <h2>Ramp-up progressif</h2>
  <p style="color:var(--muted);margin-bottom:16px;font-size:12px">Évolution des métriques quand la concurrence augmente par paliers.</p>
  <div class="charts-2col">
    <div class="chart-wrap"><canvas id="ramp-rps-chart"></canvas></div>
    <div class="chart-wrap"><canvas id="ramp-p95-chart"></canvas></div>
  </div>
  <div class="charts-2col">
    <div class="chart-wrap"><canvas id="ramp-p99-chart"></canvas></div>
    <div class="chart-wrap"><canvas id="ramp-err-chart"></canvas></div>
  </div>
  <h3>Détail par palier</h3>
  <div class="ramp-grid" id="ramp-steps"></div>
</div>

<div class="section" id="sec-regression">
  <h2>Comparaison de runs</h2>
  <p style="color:var(--muted);margin-bottom:16px;font-size:12px" id="reg-subtitle"></p>
  <div id="reg-cards"></div>
</div>

</main>

<script>
const STATS = {{statsJson}};
const RAMP  = {{rampJson}};
const REG   = {{regJson}};
const BASE_LABEL = {{(baselineLabel != null ? $"\"{baselineLabel}\"" : "null")}};

document.getElementById('run-date').textContent = new Date().toLocaleString('fr-FR');

function showTab(id, btn) {
  document.querySelectorAll('.section').forEach(s => s.classList.remove('active'));
  document.querySelectorAll('nav button').forEach(b => b.classList.remove('active'));
  document.getElementById('sec-' + id).classList.add('active');
  btn.classList.add('active');
}

const fmt      = (v, d=0) => v == null ? '-' : v.toFixed(d);
const colorFor = v => v == null ? '' : v >= 300 ? 'color:var(--red)' : v >= 100 ? 'color:var(--yellow)' : 'color:var(--green)';

const CHART_OPT = {
  plugins: { legend: { labels: { color: '#6b7280', font: { size: 11, family: 'SF Mono,Fira Code,monospace' } } } },
  scales: {
    x: { ticks: { color: '#9ca3af', font: { size: 11 } }, grid: { color: '#f0f2f6' } },
    y: { ticks: { color: '#9ca3af', font: { size: 11 } }, grid: { color: '#f0f2f6' } }
  }
};

// ── OVERVIEW ──────────────────────────────────────────────────────────────────
(function buildOverview() {
  const totalReq = STATS.reduce((a,s) => a + s.totalRequests, 0);
  const totalErr = STATS.reduce((a,s) => a + s.errorCount, 0);
  const avgP95   = STATS.reduce((a,s) => a + s.p95Ms, 0) / STATS.length;
  const avgRPS   = STATS.reduce((a,s) => a + s.rps, 0);

  document.getElementById('global-kpis').innerHTML = `
    <div class="kpi"><div class="label">Endpoints</div><div class="value" style="color:var(--accent)">${STATS.length}</div></div>
    <div class="kpi"><div class="label">Total requêtes</div><div class="value">${totalReq.toLocaleString()}</div></div>
    <div class="kpi"><div class="label">Erreurs</div><div class="value" style="${totalErr>0?'color:var(--red)':'color:var(--green)'}">${totalErr}</div></div>
    <div class="kpi"><div class="label">RPS total</div><div class="value">${fmt(avgRPS,1)}<span class="unit">req/s</span></div></div>
    <div class="kpi"><div class="label">P95 moyen</div><div class="value" style="${colorFor(avgP95)}">${fmt(avgP95,0)}<span class="unit">ms</span></div></div>
  `;

  const tbody = document.getElementById('overview-tbody');
  STATS.forEach(s => {
    const errPct = s.totalRequests > 0 ? s.errorCount / s.totalRequests * 100 : 0;
    tbody.innerHTML += `<tr>
      <td class="ep-name">${s.name}</td>
      <td>${s.totalRequests}</td>
      <td style="${errPct>0?'color:var(--red)':'color:var(--green)'}">${s.errorCount} (${fmt(errPct,1)}%)</td>
      <td>${fmt(s.rps,1)}</td>
      <td style="${colorFor(s.meanMs)}">${fmt(s.meanMs,0)} ms</td>
      <td style="${colorFor(s.p50Ms)}">${fmt(s.p50Ms,0)} ms</td>
      <td style="${colorFor(s.p95Ms)}">${fmt(s.p95Ms,0)} ms</td>
      <td style="${colorFor(s.p99Ms)}">${fmt(s.p99Ms,0)} ms</td>
      <td style="${colorFor(s.maxMs)}">${fmt(s.maxMs,0)} ms</td>
    </tr>`;
  });
})();

// ── LATENCY ───────────────────────────────────────────────────────────────────
(function buildLatency() {
  const container = document.getElementById('latency-charts');
  STATS.forEach(s => {
    const histKeys = Object.keys(s.latencyHistogram || {});
    const histVals = histKeys.map(k => s.latencyHistogram[k]);
    const safeId   = s.name.replace(/\W/g, '_');

    container.innerHTML += `
      <h3>${s.name}</h3>
      <div class="charts-2col">
        <div class="chart-wrap"><canvas id="hist-${safeId}"></canvas></div>
        <div class="chart-wrap"><canvas id="pct-${safeId}"></canvas></div>
      </div>`;

    setTimeout(() => {
      new Chart(document.getElementById('hist-' + safeId), {
        type: 'bar',
        data: { labels: histKeys, datasets: [{ label: 'Requêtes', data: histVals,
          backgroundColor: '#4f46e520', borderColor: '#4f46e5', borderWidth: 1 }] },
        options: { ...CHART_OPT, plugins: { ...CHART_OPT.plugins,
          title: { display: true, text: 'Histogramme des latences', color: '#6b7280', font: { size: 11 } } } }
      });
      new Chart(document.getElementById('pct-' + safeId), {
        type: 'bar',
        data: {
          labels: ['Min', 'P50', 'Mean', 'P95', 'P99', 'Max'],
          datasets: [{ label: 'ms',
            data: [s.minMs, s.p50Ms, s.meanMs, s.p95Ms, s.p99Ms, s.maxMs],
            backgroundColor: ['#16a34a20','#4f46e520','#4f46e540','#d9770640','#dc262640','#dc2626'],
            borderColor:     ['#16a34a',  '#4f46e5',  '#4f46e5',  '#d97706',  '#dc2626',  '#dc2626'],
            borderWidth: 1 }]
        },
        options: { ...CHART_OPT, plugins: { ...CHART_OPT.plugins,
          title: { display: true, text: 'Percentiles', color: '#6b7280', font: { size: 11 } } } }
      });
    }, 0);
  });
})();

// ── RAMP-UP ───────────────────────────────────────────────────────────────────
if (RAMP) {
  document.getElementById('tab-rampup').style.display = '';

  const labels = RAMP.map(r => `c=${r.concurrency}`);
  const mkLine = (label, data, color) => ({
    label, data, borderColor: color, backgroundColor: color + '15',
    fill: false, tension: 0.3, pointRadius: 4, pointBackgroundColor: color
  });

  setTimeout(() => {
    new Chart(document.getElementById('ramp-rps-chart'), { type: 'line',
      data: { labels, datasets: [ mkLine('RPS', RAMP.map(r => r.stats.rps), '#16a34a') ] },
      options: { ...CHART_OPT, plugins: { ...CHART_OPT.plugins,
        title: { display: true, text: 'Throughput (RPS)', color: '#6b7280', font: { size: 11 } } } }
    });
    new Chart(document.getElementById('ramp-p95-chart'), { type: 'line',
      data: { labels, datasets: [ mkLine('P95 ms', RAMP.map(r => r.stats.p95Ms), '#4f46e5') ] },
      options: { ...CHART_OPT, plugins: { ...CHART_OPT.plugins,
        title: { display: true, text: 'P95 Latence (ms)', color: '#6b7280', font: { size: 11 } } } }
    });
    new Chart(document.getElementById('ramp-p99-chart'), { type: 'line',
      data: { labels, datasets: [ mkLine('P99 ms', RAMP.map(r => r.stats.p99Ms), '#d97706') ] },
      options: { ...CHART_OPT, plugins: { ...CHART_OPT.plugins,
        title: { display: true, text: 'P99 Latence (ms)', color: '#6b7280', font: { size: 11 } } } }
    });
    new Chart(document.getElementById('ramp-err-chart'), { type: 'line',
      data: { labels, datasets: [ mkLine('Erreurs %', RAMP.map(r => r.stats.errorRate), '#dc2626') ] },
      options: { ...CHART_OPT, plugins: { ...CHART_OPT.plugins,
        title: { display: true, text: "Taux d'erreur (%)", color: '#6b7280', font: { size: 11 } } } }
    });
  }, 0);

  const stepsEl = document.getElementById('ramp-steps');
  RAMP.forEach((r, i) => {
    const p95Color = r.stats.p95Ms > 500 ? '#f87171' : r.stats.p95Ms > 200 ? '#fbbf24' : '#4ade80';
    stepsEl.innerHTML += `<div class="ramp-step${i===0?' selected':''}" onclick="selectStep(${i})">
      <div class="conc">${r.concurrency}</div>
      <div class="sub">concurrent</div>
      <div class="rps">${r.stats.rps.toFixed(1)} RPS</div>
      <div class="sub" style="color:${p95Color}">P95: ${r.stats.p95Ms.toFixed(0)}ms</div>
    </div>`;
  });
}

function selectStep(i) {
  document.querySelectorAll('.ramp-step').forEach((el, j) => el.classList.toggle('selected', i === j));
}

// ── REGRESSION ────────────────────────────────────────────────────────────────
if (REG) {
  document.getElementById('tab-regression').style.display = '';
  if (BASE_LABEL)
    document.getElementById('reg-subtitle').textContent = `Baseline : ${BASE_LABEL}`;

  const sevClass = s => s === 2 ? 'crit' : s === 1 ? 'warn' : 'ok';
  const sevLabel = s => s === 2 ? 'CRITICAL' : s === 1 ? 'WARNING' : 'OK';

  const cards = document.getElementById('reg-cards');
  REG.forEach(ep => {
    const sev = ep.overallSeverity;
    let rows = '';
    ep.diffs.forEach(d => {
      const delta  = d.baseline === 0 ? 0 : (d.current - d.baseline) / d.baseline * 100;
      const barMax = d.unit === 'ms'
        ? Math.max(...ep.diffs.filter(x => x.unit === 'ms').map(x => x.current), 1) : 100;
      const barPct  = Math.min(100, d.current / barMax * 100);
      const barColor = d.severity === 2 ? 'var(--red)' : d.severity === 1 ? 'var(--yellow)' : 'var(--green)';
      const deltaStr = d.unit === 'req/s' || d.unit === 'ms'
        ? `${delta >= 0 ? '+' : ''}${delta.toFixed(1)}%`
        : `${delta >= 0 ? '+' : ''}${(d.current - d.baseline).toFixed(2)}pp`;
      rows += `<div class="diff-row">
        <span class="diff-metric">${d.metric}</span>
        <div class="diff-bar-wrap"><div class="diff-bar" style="width:${barPct}%;background:${barColor}"></div></div>
        <span class="diff-delta ${d.isRegression ? 'delta-pos' : 'delta-neg'}">${deltaStr}</span>
        <span class="diff-vals">${d.baseline} → ${d.current} ${d.unit}</span>
        <span><span class="badge ${sevClass(d.severity)}">${sevLabel(d.severity)}</span></span>
      </div>`;
    });
    cards.innerHTML += `<div class="reg-card">
      <div class="reg-header">
        <span class="reg-ep">${ep.endpointName}</span>
        <span class="badge ${sevClass(sev)}">${sevLabel(sev)}</span>
      </div>
      ${rows}
    </div>`;
  });
}
</script>
</body>
</html>
""";
    }
}
