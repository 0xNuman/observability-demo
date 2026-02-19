# Grafana Drilldown Playbook

Use this workflow when you detect a latency spike, increased errors, or a drop in reliability.

## Fast Path (Metric -> Trace -> Logs)

When exemplar data is present, use this shortest path:
1. In a Prometheus timeseries panel, click an exemplar marker (dot) near the spike.
2. Grafana opens the corresponding Tempo trace.
3. From trace view, use trace-to-logs to open matching Loki logs.

This is the primary low-friction workflow for incident triage.

## 1. Start from Dashboard Signals

Open Grafana dashboard: `Reliability And Drilldown`.

Set:
- Time range: incident window (for example `Last 15m`).
- `instance`: `All` (narrow later only if needed).

Check these panels first:
- `Top Slow Routes p95 (ms, 5m)`
- `Slow Requests > 500ms By Route (req/s)`
- `5xx Rate By Route (req/s)`

Goal: identify the affected route and a specific spike timestamp.

## 2. Move to Traces (Tempo)

Open **Explore -> Tempo**.

Search with:
- `service.name = observability-demo-api`
- Time window around the spike
- For slow requests: add duration filter (for example `> 500ms`)
- For errors: focus on failed spans / 5xx traces

Open one representative slow or failed trace and inspect the span waterfall.

Capture:
- `traceid`
- `service.instance.id` (or instance identifier)
- `tenant_id` (if present)
- slowest span name and duration

Goal: locate where time is spent or where failure starts.

## 3. Pivot to Logs (Loki)

Option A:
- From `Error Logs (Click trace_id to open Tempo trace)` panel, click a trace link.

Option B:
- In **Explore -> Loki**, query directly using route/time context.

Base query:

```logql
{service_name="observability-demo-api"} | json
```

Route-focused:

```logql
{service_name="observability-demo-api"} | json | attributes_RequestPath="/your/route"
```

Error-focused:

```logql
{service_name="observability-demo-api"} | json | attributes_RequestPath="/your/route" | attributes_StatusCode >= 500
```

Trace-focused:

```logql
{service_name="observability-demo-api"} | json | traceid="your-trace-id"
```

Then refine with:
- `tenant_id`
- `instance_id`
- `attributes_StatusCode`

Goal: find concrete error/timeout evidence explaining the trace behavior.

## 4. Confirm Correlation

Validate all three layers align:
- Same time window
- Same route
- Same trace and/or instance
- Same tenant (if incident is tenant-specific)

If they align, you have a defensible root-cause chain:
metric anomaly -> trace slowdown/failure -> log evidence.

## 5. Useful Investigation Patterns

Latency incident:
1. Route spike in p95/p99 panel.
2. Slow trace in Tempo.
3. Long span indicates bottleneck (DB call, external dependency, serialization, etc.).
4. Logs confirm retries/timeouts or downstream slowness.

Error-rate incident:
1. `5xx Rate By Route` rises.
2. Failed traces show failing span/service segment.
3. Logs confirm exception type and failing operation.

Instance-specific incident:
1. Set dashboard `instance` to one value.
2. Compare route/error behavior with `All`.
3. Validate in logs with matching `instance_id`.
