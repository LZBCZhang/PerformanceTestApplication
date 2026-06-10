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
        var rampJson = rampUp != null ? JsonSerializer.Serialize(rampUp) : "null";
        var regJson = regressions != null ? JsonSerializer.Serialize(regressions) : "null";

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
    --bg: #0d0f14;
    --surface: #161923;
    --border: #252b3b;
    --text: #e2e8f0;
    --muted: #64748b;
    --accent: #6366f1;
    --green: #22c55e;
    --yellow: #f59e0b;
    --red: #ef4444;
    --font: 'SF Mono', 'Fira Code', 'Consolas', monospace;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { background: var(--bg); color: var(--text); font-family: var(--font); font-size: 13px; line-height: 1.6; }

  /* Layout */
  header { border-bottom: 1px solid var(--border); padding: 24px 40px; display: flex; align-items: baseline; gap: 16px; }
  header h1 { font-size: 18px; font-weight: 600; letter-spacing: -0.3px; color: #fff; }
  header .meta { color: var(--muted); font-size: 12px; }
  nav { display: flex; gap: 4px; padding: 16px 40px 0; border-bottom: 1px solid var(--border); }
  nav button { background: none; border: none; color: var(--muted); font: inherit; font-size: 12px;
    padding: 8px 16px; cursor: pointer; border-bottom: 2px solid transparent; margin-bottom: -1px; }
  nav button.active { color: var(--accent); border-bottom-color: var(--accent); }
  main { padding: 32px 40px; max-width: 1200px; }

  /* Sections */
  .section { display: none; } .section.active { display: block; }
  h2 { font-size: 14px; font-weight: 600; color: #fff; margin-bottom: 20px; letter-spacing: 0.5px; text-transform: uppercase; }
  h3 { font-size: 13px; font-weight: 600; color: var(--accent); margin: 28px 0 12px; }

  /* Cards métriques */
  .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; margin-bottom: 28px; }
  .kpi { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; padding: 14px 16px; }
  .kpi .label { font-size: 11px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.8px; margin-bottom: 6px; }
  .kpi .value { font-size: 22px; font-weight: 600; color: #fff; }
  .kpi .unit { font-size: 11px; color: var(--muted); margin-left: 3px; }

  /* Tableau */
  .tbl-wrap { overflow-x: auto; }
  table { width: 100%; border-collapse: collapse; font-size: 12px; }
  th { text-align: left; color: var(--muted); font-weight: 500; padding: 8px 12px; border-bottom: 1px solid var(--border); }
  td { padding: 10px 12px; border-bottom: 1px solid #1e2433; }
  tr:hover td { background: var(--surface); }
  .ep-name { color: var(--accent); font-weight: 600; }

  /* Badges */
  .badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
  .ok { background: #14532d; color: var(--green); }
  .warn { background: #451a03; color: var(--yellow); }
  .crit { background: #450a0a; color: var(--red); }

  /* Chart containers */
  .chart-wrap { background: var(--surface); border: 1px solid var(--border); border-radius: 10px;
    padding: 20px; margin-bottom: 24px; }
  .chart-wrap canvas { max-height: 300px; }
  .charts-2col { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }

  /* Regression */
  .reg-card { background: var(--surface); border: 1px solid var(--border); border-radius: 10px;
    padding: 20px; margin-bottom: 16px; }
  .reg-header { display: flex; align-items: center; gap: 10px; margin-bottom: 16px; }
  .reg-ep { font-weight: 600; color: #fff; font-size: 14px; }
  .diff-row { display: grid; grid-template-columns: 80px 1fr 80px 80px 100px; gap: 8px;
    align-items: center; padding: 6px 0; border-bottom: 1px solid #1e2433; font-size: 12px; }
  .diff-row:last-child { border-bottom: none; }
  .diff-metric { color: var(--muted); }
  .diff-bar-wrap { background: #1e2433; border-radius: 3px; height: 6px; position: relative; }
  .diff-bar { height: 6px; border-radius: 3px; }
  .diff-delta { text-align: right; }
  .delta-pos { color: var(--red); }
  .delta-neg { color: var(--green); }
  .diff-vals { color: var(--muted); font-size: 11px; }

  /* Ramp-up */
  .ramp-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 10px; margin-bottom: 20px; }
  .ramp-step { background: var(--surface); border: 1px solid var(--border); border-radius: 8px;
    padding: 12px; text-align: center; cursor: pointer; transition: border-color .15s; }
  .ramp-step:hover, .ramp-step.selected { border-color: var(--accent); }
  .ramp-step .conc { font-size: 20px; font-weight: 700; color: var(--accent); }
  .ramp-step .sub { font-size: 10px; color: var(--muted); margin-top: 2px; }
  .ramp-step .rps { font-size: 13px; font-weight: 600; color: #fff; margin-top: 4px; }

  @media (max-width: 700px) {
    header, nav, main { padding-left: 16px; padding-right: 16px; }
    .charts-2col { grid-template-columns: 1fr; }
  }
</style>
</head>
<body>

<header>
  <h1>⚡ Performance Report</h1>
  <span class="meta" id="run-label">{{runLabel}}</span>
  <span class="meta" style="margin-left:auto" id="run-date"></span>
</header>

<nav>
  <button class="active" onclick="showTab('overview')">Vue d'ensemble</button>
  <button onclick="showTab('latency')">Latences</button>
  <button id="tab-rampup" onclick="showTab('rampup')" style="display:none">Ramp-up</button>
  <button id="tab-regression" onclick="showTab('regression')" style="display:none">Régressions</button>
</nav>

<main>

<!-- ── OVERVIEW ─────────────────────────────────────────────── -->
<div class="section active" id="sec-overview">
  <h2>Résumé global</h2>
  <div class="kpi-grid" id="global-kpis"></div>
  <div class="tbl-wrap">
    <table id="overview-table">
      <thead>
        <tr>
          <th>Endpoint</th>
          <th>Requêtes</th>
          <th>Erreurs</th>
          <th>RPS</th>
          <th>Mean</th>
          <th>P50</th>
          <th>P95</th>
          <th>P99</th>
          <th>Max</th>
        </tr>
      </thead>
      <tbody id="overview-tbody"></tbody>
    </table>
  </div>
</div>

<!-- ── LATENCY ───────────────────────────────────────────────── -->
<div class="section" id="sec-latency">
  <h2>Latences par endpoint</h2>
  <div id="latency-charts"></div>
</div>

<!-- ── RAMP-UP ───────────────────────────────────────────────── -->
<div class="section" id="sec-rampup">
  <h2>Ramp-up progressif</h2>
  <p style="color:var(--muted);margin-bottom:20px;font-size:12px">
    Évolution des métriques quand la concurrence augmente par paliers.</p>
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

<!-- ── REGRESSION ────────────────────────────────────────────── -->
<div class="section" id="sec-regression">
  <h2>Comparaison de runs</h2>
  <p style="color:var(--muted);margin-bottom:20px;font-size:12px" id="reg-subtitle"></p>
  <div id="reg-cards"></div>
</div>

</main>

<script>
const STATS = {{statsJson}};
const RAMP  = {{rampJson}};
const REG   = {{regJson}};
const BASE_LABEL = {{(baselineLabel != null ? $"\"{baselineLabel}\"" : "null")}};

document.getElementById('run-date').textContent = new Date().toLocaleString('fr-FR');

// ── Tab navigation ──────────────────────────────────────────────────────────
function showTab(id) {
  document.querySelectorAll('.section').forEach(s => s.classList.remove('active'));
  document.querySelectorAll('nav button').forEach(b => b.classList.remove('active'));
  document.getElementById('sec-' + id).classList.add('active');
  event.currentTarget.classList.add('active');
}

// ── Helpers ──────────────────────────────────────────────────────────────────
const fmt = (v, d=0) => v == null ? '-' : v.toFixed(d);
const pct = v => v >= 0 ? `+${v.toFixed(1)}%` : `${v.toFixed(1)}%`;
const colorFor = v => v == null ? '' : v >= 300 ? 'color:var(--red)' : v >= 100 ? 'color:var(--yellow)' : 'color:var(--green)';

const CHART_DEFAULTS = {
  color: '#e2e8f0',
  plugins: { legend: { labels: { color: '#94a3b8', font: { size: 11, family: 'SF Mono,Fira Code,monospace' } } } },
  scales: {
    x: { ticks: { color: '#64748b', font: { size: 11 } }, grid: { color: '#1e2433' } },
    y: { ticks: { color: '#64748b', font: { size: 11 } }, grid: { color: '#1e2433' } }
  }
};

// ── OVERVIEW ─────────────────────────────────────────────────────────────────
(function buildOverview() {
  const totalReq = STATS.reduce((a,s) => a + s.totalRequests, 0);
  const totalErr = STATS.reduce((a,s) => a + s.errorCount, 0);
  const avgP95   = STATS.reduce((a,s) => a + s.p95Ms, 0) / STATS.length;
  const avgRPS   = STATS.reduce((a,s) => a + s.rPS, 0);

  document.getElementById('global-kpis').innerHTML = `
    <div class="kpi"><div class="label">Endpoints</div><div class="value">${STATS.length}</div></div>
    <div class="kpi"><div class="label">Total requêtes</div><div class="value">${totalReq.toLocaleString()}</div></div>
    <div class="kpi"><div class="label">Erreurs</div><div class="value" style="${totalErr>0?'color:var(--red)':'color:var(--green)'}">${totalErr}</div></div>
    <div class="kpi"><div class="label">RPS total</div><div class="value">${fmt(avgRPS,1)}<span class="unit">req/s</span></div></div>
    <div class="kpi"><div class="label">P95 moyen</div><div class="value" style="${colorFor(avgP95)}">${fmt(avgP95,0)}<span class="unit">ms</span></div></div>
  `;

  const tbody = document.getElementById('overview-tbody');
  STATS.forEach(s => {
    const errPct = (s.totalRequests > 0 ? s.errorCount/s.totalRequests*100 : 0);
    tbody.innerHTML += `<tr>
      <td class="ep-name">${s.name}</td>
      <td>${s.totalRequests}</td>
      <td style="${errPct>0?'color:var(--red)':''}">${s.errorCount} (${fmt(errPct,1)}%)</td>
      <td>${fmt(s.rPS,1)}</td>
      <td style="${colorFor(s.meanMs)}">${fmt(s.meanMs,0)} ms</td>
      <td style="${colorFor(s.p50Ms)}">${fmt(s.p50Ms,0)} ms</td>
      <td style="${colorFor(s.p95Ms)}">${fmt(s.p95Ms,0)} ms</td>
      <td style="${colorFor(s.p99Ms)}">${fmt(s.p99Ms,0)} ms</td>
      <td style="${colorFor(s.maxMs)}">${fmt(s.maxMs,0)} ms</td>
    </tr>`;
  });
})();

// ── LATENCY charts ────────────────────────────────────────────────────────────
(function buildLatency() {
  const container = document.getElementById('latency-charts');
  STATS.forEach(s => {
    const histKeys = Object.keys(s.latencyHistogram || {});
    const histVals = histKeys.map(k => s.latencyHistogram[k]);

    container.innerHTML += `
      <h3>${s.name}</h3>
      <div class="charts-2col">
        <div class="chart-wrap">
          <canvas id="hist-${s.name.replace(/\W/g,'_')}"></canvas>
        </div>
        <div class="chart-wrap">
          <canvas id="pct-${s.name.replace(/\W/g,'_')}"></canvas>
        </div>
      </div>`;

    setTimeout(() => {
      // Histogramme
      new Chart(document.getElementById('hist-' + s.name.replace(/\W/g,'_')), {
        type: 'bar',
        data: {
          labels: histKeys,
          datasets: [{ label: 'Requêtes', data: histVals,
            backgroundColor: '#6366f150', borderColor: '#6366f1', borderWidth: 1 }]
        },
        options: { ...CHART_DEFAULTS, plugins: { ...CHART_DEFAULTS.plugins,
          title: { display: true, text: 'Histogramme des latences', color: '#94a3b8', font: { size: 12 } } } }
      });

      // Percentiles bar
      new Chart(document.getElementById('pct-' + s.name.replace(/\W/g,'_')), {
        type: 'bar',
        data: {
          labels: ['Min', 'P50', 'Mean', 'P95', 'P99', 'Max'],
          datasets: [{ label: 'ms',
            data: [s.minMs, s.p50Ms, s.meanMs, s.p95Ms, s.p99Ms, s.maxMs],
            backgroundColor: ['#22c55e40','#6366f140','#6366f180','#f59e0b80','#ef444480','#ef4444'],
            borderColor:     ['#22c55e','#6366f1','#6366f1','#f59e0b','#ef4444','#ef4444'],
            borderWidth: 1 }]
        },
        options: { ...CHART_DEFAULTS, plugins: { ...CHART_DEFAULTS.plugins,
          title: { display: true, text: 'Percentiles', color: '#94a3b8', font: { size: 12 } } } }
      });
    }, 0);
  });
})();

// ── RAMP-UP ───────────────────────────────────────────────────────────────────
if (RAMP) {
  document.getElementById('tab-rampup').style.display = '';

  const labels = RAMP.map(r => `c=${r.concurrency}`);
  const mkLine = (label, data, color) => ({
    label, data, borderColor: color, backgroundColor: color + '20',
    fill: false, tension: 0.3, pointRadius: 4
  });

  setTimeout(() => {
    new Chart(document.getElementById('ramp-rps-chart'), { type: 'line',
      data: { labels, datasets: [ mkLine('RPS', RAMP.map(r=>r.stats.rPS), '#22c55e') ] },
      options: { ...CHART_DEFAULTS, plugins: { ...CHART_DEFAULTS.plugins,
        title: { display:true, text:'Throughput (RPS)', color:'#94a3b8', font:{size:12} } } }
    });
    new Chart(document.getElementById('ramp-p95-chart'), { type: 'line',
      data: { labels, datasets: [ mkLine('P95 ms', RAMP.map(r=>r.stats.p95Ms), '#6366f1') ] },
      options: { ...CHART_DEFAULTS, plugins: { ...CHART_DEFAULTS.plugins,
        title: { display:true, text:'P95 Latence (ms)', color:'#94a3b8', font:{size:12} } } }
    });
    new Chart(document.getElementById('ramp-p99-chart'), { type: 'line',
      data: { labels, datasets: [ mkLine('P99 ms', RAMP.map(r=>r.stats.p99Ms), '#f59e0b') ] },
      options: { ...CHART_DEFAULTS, plugins: { ...CHART_DEFAULTS.plugins,
        title: { display:true, text:'P99 Latence (ms)', color:'#94a3b8', font:{size:12} } } }
    });
    new Chart(document.getElementById('ramp-err-chart'), { type: 'line',
      data: { labels, datasets: [ mkLine('Erreurs %', RAMP.map(r=>r.stats.errorRate), '#ef4444') ] },
      options: { ...CHART_DEFAULTS, plugins: { ...CHART_DEFAULTS.plugins,
        title: { display:true, text:"Taux d'erreur (%)", color:'#94a3b8', font:{size:12} } } }
    });
  }, 0);

  const stepsEl = document.getElementById('ramp-steps');
  RAMP.forEach((r, i) => {
    stepsEl.innerHTML += `<div class="ramp-step${i===0?' selected':''}" onclick="selectStep(${i})">
      <div class="conc">${r.concurrency}</div>
      <div class="sub">concurrent</div>
      <div class="rps">${r.stats.rPS.toFixed(1)} RPS</div>
      <div class="sub" style="color:${r.stats.p95Ms>500?'var(--red)':r.stats.p95Ms>200?'var(--yellow)':'var(--green)'}">
        P95: ${r.stats.p95Ms.toFixed(0)}ms</div>
    </div>`;
  });
}

function selectStep(i) {
  document.querySelectorAll('.ramp-step').forEach((el,j) => {
    el.classList.toggle('selected', i===j);
  });
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
      const delta = d.baseline === 0 ? 0 : (d.current - d.baseline) / d.baseline * 100;
      const isLatency = d.unit === 'ms';
      const barMax = isLatency ? Math.max(...ep.diffs.filter(x=>x.unit==='ms').map(x=>x.current), 1) : 100;
      const barPct = Math.min(100, d.current / barMax * 100);
      const barColor = d.severity === 2 ? 'var(--red)' : d.severity === 1 ? 'var(--yellow)' : 'var(--green)';
      const deltaStr = d.unit === 'req/s'
        ? `${delta>=0?'+':''}${delta.toFixed(1)}%`
        : d.unit === '%'
          ? `${delta>=0?'+':''}${(d.current-d.baseline).toFixed(2)}pp`
          : `${delta>=0?'+':''}${delta.toFixed(1)}%`;
      const deltaClass = d.isRegression ? 'delta-pos' : 'delta-neg';
      rows += `<div class="diff-row">
        <span class="diff-metric">${d.metric}</span>
        <div class="diff-bar-wrap"><div class="diff-bar" style="width:${barPct}%;background:${barColor}"></div></div>
        <span class="diff-delta ${deltaClass}">${deltaStr}</span>
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
