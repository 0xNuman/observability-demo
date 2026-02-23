# Grafana Drilldown Playbook

Use this workflow when you detect a latency spike, increased errors, or a drop in reliability.

## Fast Path: Exemplar-Driven Investigation (Metric → Trace → Logs)

This is the primary low-friction workflow for incident triage. When exemplar data is present, you can traverse all three observability pillars in seconds.

### Step 1: Spot the anomaly on a dashboard panel

Open either the **SRE Overview** or **Reliability And Drilldown** dashboard. Look for:
- A spike in the latency percentiles timeseries
- An increase in the error rate panel
- A jump in the request duration heatmap

### Step 2: Click an exemplar marker

On any exemplar-enabled timeseries panel (p50/p95/p99 latency, error rate by route, exemplar clickthrough), look for small diamond-shaped dots near the data line. Each dot represents a single traced request.

1. Hover over a dot near the spike to see its trace ID and value.
2. Click the dot — Grafana opens the trace directly in Tempo.

### Step 3: Inspect the trace in Tempo

In the Tempo trace view:
- Examine the span waterfall to identify where time was spent.
- Look for long spans (database queries, external calls) or error spans.
- Note the `service.instance.id` and `tenant_id` tags for filtering.

### Step 4: Pivot to logs

From the Tempo trace view, click **"Logs for this trace"** in the top bar. The Tempo datasource is configured with `tracesToLogsV2` including:
- `filterByTraceID: true` — auto-filters Loki to the exact trace ID
- Tag mappings for `service.name` → `service_name` and `service.instance.id` → `instance_id`

This opens Loki pre-filtered to show only log lines from that specific traced request.

### Step 5: Read the evidence

The Loki logs will show:
- The structured JSON log entries with `trace_id`, `span_id`, `tenant_id`, `instance_id`
- Exception messages and stack traces for error cases
- Request path, HTTP status code, and timing information

You now have a complete chain: **metric anomaly → specific trace → correlated logs**.

---

## 1. Start from Dashboard Signals

Open Grafana dashboard: `Reliability And Drilldown`.

Set:
- Time range: incident window (for example `Last 15m`).
- `instance`: `All` (narrow later only if needed).
- `route`: `All` or a specific suspected route.

Check these panels first:
- `Top Slow Routes p95 (ms, 5m)`
- `Slow Requests > 500ms By Route (req/s)`
- `5xx Rate By Route (req/s)`
- `Request Rate per Route (req/s)` — check for traffic drops or spikes

Goal: identify the affected route and a specific spike timestamp.

## 2. Move to Traces (Tempo)

Open **Explore → Tempo**.

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

Option A — from trace view:
- Click **"Logs for this trace"** in Tempo (uses tracesToLogsV2 with filterByTraceID)

Option B — from error logs panel:
- From `Error Logs (Click trace_id to open Tempo trace)` panel, click a trace link.

Option C — manual Loki query:

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
metric anomaly → trace slowdown/failure → log evidence.

## 5. Useful Investigation Patterns

### Latency incident
1. Route spike in p95/p99 panel or heatmap shows requests shifting to higher buckets.
2. Slow trace in Tempo.
3. Long span indicates bottleneck (DB call, external dependency, serialization, etc.).
4. Logs confirm retries/timeouts or downstream slowness.

### Error-rate incident
1. `5xx Rate By Route` rises.
2. Failed traces show failing span/service segment.
3. Logs confirm exception type and failing operation.

### Instance-specific incident
1. Set dashboard `instance` to one value.
2. Compare route/error behavior with `All`.
3. Validate in logs with matching `instance_id`.

### Connection pool exhaustion
1. Check the **Database Connection Pool** row in Reliability Drilldown.
2. Look for `Pending Connection Requests` climbing above zero.
3. Correlation: high pending requests + rising `Connection Create Time p95` = pool saturation.
4. Cross-reference with the **Thread Pool Queue Length** panel — thread starvation and connection pool exhaustion often co-occur.

### Thread pool starvation
1. Check the **.NET Runtime** row in Reliability Drilldown.
2. `Thread Pool Queue Length` rising above 20 is a warning sign; above 50 is critical.
3. This often manifests as rising p99 latency across all routes simultaneously.
4. Look for synchronous-over-async patterns in the trace waterfall.

## 6. Troubleshooting

### Exemplar dots not appearing on panels
- Verify traffic is flowing: check that the request rate stat panel shows > 0 req/s.
- Exemplars require active traces. Confirm the API's OTel tracing is configured (`AddAspNetCoreInstrumentation` in `ApiObservabilityExtensions.cs`).
- Confirm Prometheus has exemplar storage enabled: check `--enable-feature=exemplar-storage` in compose.yaml.
- Confirm the panel target has `"exemplar": true` set.
- Exemplars are sampled — not every request produces one. Generate more traffic with `ops/scripts/generate-traffic.sh`.

### Clicking exemplar does not open Tempo
- Check the Prometheus datasource has `exemplarTraceIdDestinations` pointing to the `tempo` datasource UID.
- Verify Tempo is receiving traces: query Tempo directly at `http://localhost:3200/api/search`.

### "Logs for this trace" shows empty results in Loki
- The Tempo datasource `tracesToLogsV2` must have `filterByTraceID: true` and the correct tag mappings. Verify in `ops/grafana/provisioning/datasources/datasources.yml`.
- Check that logs are reaching Loki: query `{service_name="observability-demo-api"} | json` in Explore → Loki.
- Verify the trace ID format matches between Tempo and Loki. The Serilog OTel sink writes `trace_id` as a 32-character hex string.

### Recording rules show no data
- Check Prometheus loaded the rules: `curl -s localhost:9090/api/v1/rules | jq '.data.groups[].name'`.
- Ensure there is traffic generating the base `http_server_request_duration_seconds` metric.
- Verify `exported_job="observability-demo-api"` label exists by querying `http_server_request_duration_seconds_count` in Prometheus.

### DB connection pool metrics not appearing
- Verify `Npgsql.OpenTelemetry` is referenced in the Infrastructure project.
- Confirm `.AddMeter("Npgsql")` is present in `ApiObservabilityExtensions.cs`.
- Check `NpgsqlDataSourceBuilder.UseOpenTelemetry()` is called in `DependencyInjection.cs`.
- Query `{__name__=~"db_client.*"}` in Prometheus to verify the metrics exist.
