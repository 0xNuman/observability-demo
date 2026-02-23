# SRE Dashboard Guide

This guide provides a comprehensive walkthrough of the two Grafana dashboards in the observability demo, the metrics they consume, the SLO framework that drives alerting, and the end-to-end exemplar workflow that connects a single metric data point to its distributed trace and correlated logs.

Open Grafana at **http://localhost:3000** (admin / admin).

---

## Table of Contents

1. [Google SRE Golden Signals Philosophy](#1-google-sre-golden-signals-philosophy)
2. [SRE Overview Dashboard](#2-sre-overview-dashboard)
3. [Reliability And Drilldown Dashboard](#3-reliability-and-drilldown-dashboard)
4. [Metric Reference Table](#4-metric-reference-table)
5. [End-to-End Exemplar Workflow](#5-end-to-end-exemplar-workflow)
6. [SLO Framework](#6-slo-framework)
7. [Alert Severity Model and Response Playbook](#7-alert-severity-model-and-response-playbook)

---

## 1. Google SRE Golden Signals Philosophy

Google's Site Reliability Engineering book defines four golden signals that every service should monitor. These dashboards are built around them.

| Signal | What It Measures | Why It Matters |
|--------|-----------------|----------------|
| **Latency** | Time to serve a request (both successful and failed) | Users feel latency first. A p99 spike that affects 1% of users can still mean thousands of degraded experiences per hour. |
| **Traffic** | Request volume (req/s) | Establishes the baseline demand. A sudden drop may indicate an upstream failure; a spike may precede saturation. |
| **Errors** | Fraction of requests returning 5xx | Directly measures whether the system is doing its job. Error rate is the numerator of the availability SLI. |
| **Saturation** | How "full" a resource is (thread pool, connection pool, memory) | The leading indicator. By the time latency or errors move, saturation has usually been elevated for a while. Catching it early prevents incidents. |

Both dashboards map directly to these signals:

- **SRE Overview** focuses on Latency, Traffic, and Errors at the service level, plus SLO burn rates.
- **Reliability And Drilldown** adds Saturation (.NET runtime, DB connection pool) and deep per-route investigation tools.

---

## 2. SRE Overview Dashboard

**UID:** `latency-overview-bootstrap` | **Default range:** last 1 hour | **Refresh:** 10s

**Template variables:** `$instance` (API container hostname), `$route` (HTTP route pattern). Both support multi-select and "All".

### Row 1 -- Golden Signals

Four stat panels providing an instant health summary.

| Panel | PromQL (simplified) | Why It Matters |
|-------|---------------------|----------------|
| **Request Rate (req/s)** | `sum(rate(http_server_request_duration_seconds_count[5m]))` | Answers "is traffic normal?" A sudden drop can indicate DNS failure, load balancer misconfiguration, or upstream caller crash. A spike can precede saturation. |
| **Error Rate %** | `100 * sum(rate(...{status=~"5.."}[5m])) / sum(rate(...[5m]))` | The single most important number. Thresholds: green < 1%, yellow 1-5%, red > 5%. If this panel turns red, you are actively degrading user experience. |
| **p95 Latency** | `histogram_quantile(0.95, sum by (le) (rate(..._bucket[5m])))` | Captures tail latency that medians miss. Thresholds: green < 500ms, yellow 500ms-2s, red > 2s. The 500ms boundary aligns with the latency SLO. |
| **Availability %** | `100 * sum(rate(...{status!~"5.."}[5m])) / sum(rate(...[5m]))` | Real-time availability over 5 minutes. Thresholds: red < 99%, yellow 99-99.9%, green >= 99.9%. Turning yellow means you are approaching your 99.9% SLO target. |

**How to read this row:** If all four panels are green, you can move on to other work. If any is yellow or red, drill into the rows below to identify which routes and instances are contributing.

### Row 2 -- Traffic & Latency

Three timeseries panels with exemplar dots enabled.

| Panel | What It Shows | Why It Matters |
|-------|---------------|----------------|
| **Latency Percentiles (p50 / p95 / p99)** | Three lines tracking median, 95th, and 99th percentile response times over time. Exemplar dots overlay individual request trace IDs. | Reveals whether latency degradation is broad (all percentiles move) or tail-only (p99 spikes while p50 is flat). The exemplar dots let you click directly to the Tempo trace for any outlier request. |
| **Request Rate by Route** | Stacked area chart of per-route request rate. | Shows traffic distribution. If `/work-items` accounts for 80% of traffic, a latency regression there has 4x the user impact of `/work-items/{id}`. Stacking reveals total throughput at a glance. |
| **Error Rate by Route** | Per-route 5xx error rate. Exemplar dots enabled. | Pinpoints which endpoint is failing. During an incident, this panel tells you immediately whether the problem is isolated (one route) or systemic (all routes). |

### Row 3 -- SLO Burn Rate

Six panels that implement the Google SRE multi-window burn rate model.

| Panel | Type | What It Shows | Why It Matters |
|-------|------|---------------|----------------|
| **Availability Burn Rate (1h)** | Gauge | 1-hour windowed availability burn rate. Scale: 0-20x. Green < 1x, yellow 1-6x, orange 6-14.4x, red > 14.4x. | A burn rate of 1x means you are consuming your error budget at exactly the sustainable pace. At 14.4x, the entire 30-day budget burns in under 50 hours. The gauge gives you an instant visceral read on budget health. |
| **Latency Burn Rate (1h)** | Gauge | Same model applied to the latency SLO (99% within 500ms). | Latency budget burns are subtler than availability ones. This gauge catches slow creep that raw p95 charts might not make obvious. |
| **Availability Budget Remaining** | Stat | Percentage of 30-day availability error budget remaining. Red < 25%, yellow 25-75%, green > 75%. | The ultimate "should I worry?" number. Below 25% means you have limited room for further errors before violating the SLO for the period. |
| **Latency Budget Remaining** | Stat | Percentage of 30-day latency error budget remaining. | Same logic applied to latency. When this drops below 50%, defer risky deployments. |
| **Burn Rate Over Time (All Windows)** | Timeseries | Overlays availability 5m/1h and latency 5m/1h burn rates on a single chart. Red threshold line at 14.4x. | Correlates short-window (5m) spikes with sustained (1h) trends. A 5m spike that does not appear in the 1h line is transient. A 1h line rising toward 14.4x is a developing incident. |

### Row 4 -- Request Duration Heatmap

| Panel | What It Shows | Why It Matters |
|-------|---------------|----------------|
| **Request Duration Heatmap** | Full-width heatmap of request durations using histogram bucket boundaries (5ms to 10s, 14 custom boundaries). Color intensity (Oranges scheme) indicates volume per bucket per minute. | Heatmaps reveal distribution shapes that percentile lines cannot. A bimodal distribution (fast requests + a cluster at 2s) appears as two hot bands. This pattern is invisible in a p95 chart but immediately obvious in a heatmap. Useful for identifying cache-miss populations, connection pool exhaustion patterns, or GC pauses. |

---

## 3. Reliability And Drilldown Dashboard

**UID:** `reliability-drilldown` | **Default range:** last 6 hours | **Refresh:** 10s

This dashboard is designed for incident investigation and capacity planning. It goes deeper than the SRE Overview into per-route breakdowns, infrastructure saturation, and the exemplar-driven trace/log correlation workflow.

**Template variables:** Same `$instance` and `$route` as the SRE Overview.

### SLI Stats Row (y: 0)

Four stat panels providing the 30-day window service level indicators.

| Panel | PromQL (simplified) | Why It Matters |
|-------|---------------------|----------------|
| **Availability SLI (30d)** | `100 * sum(increase(...{status!~"5.."}[30d])) / sum(increase(...[30d]))` | The official SLO measurement. This is the number you report to stakeholders. Thresholds: red < 99%, yellow 99-99.9%, green >= 99.9%. |
| **Error Rate SLI (30d)** | `100 * sum(increase(...{status=~"5.."}[30d])) / sum(increase(...[30d]))` | The complement of availability. Easier to reason about in absolute terms ("we had 0.05% errors") than the availability framing. Thresholds: green < 0.5%, yellow 0.5-1%, red > 1%. |
| **Latency SLI <= 500ms (30d)** | `100 * sum(increase(..._bucket{le="0.5",route!="diagnostics/slow"}[30d])) / sum(increase(..._count{route!="diagnostics/slow"}[30d]))` | Measures the fraction of requests served within the 500ms SLO threshold. Excludes the `/diagnostics/slow` endpoint since it is intentionally slow for testing. Thresholds: red < 95%, yellow 95-99%, green >= 99%. |
| **Uptime % (24h)** | `100 * avg(avg_over_time(up{job="api-uptime"}[24h]))` | Synthetic uptime from Prometheus scraping the `/health/prometheus` endpoint. A separate signal from request-based availability -- captures total outages where no requests are served at all. |

### Per-Route Latency and Slow Requests (y: 6)

| Panel | What It Shows | Why It Matters |
|-------|---------------|----------------|
| **p95 Latency By Route** | Timeseries with one line per HTTP route, exemplar dots enabled. | During an investigation, this panel answers "which endpoint is slow?" without needing to filter. Exemplar dots on the line let you click through to the exact trace that caused the spike. |
| **Slow Requests > 500ms By Route (req/s)** | Rate of requests exceeding the 500ms SLO boundary, broken down by route. Exemplar dots enabled. | Directly measures the rate at which you are consuming latency error budget, per route. If this panel shows 2 req/s for `/work-items`, that is 2 requests per second violating the SLO. |

### Traffic Distribution and Duration Shape (y: 14)

| Panel | What It Shows | Why It Matters |
|-------|---------------|----------------|
| **Request Rate per Route (req/s)** | Stacked area chart of per-route throughput. | Provides context for the latency panels above. A route with 0.1 req/s and high p95 is less impactful than a route with 50 req/s and moderate p95. |
| **Request Duration Heatmap** | Same heatmap visualization as the SRE Overview, scoped to the selected filters. | Placed adjacent to the RPS chart so you can visually correlate traffic changes with duration distribution shifts. |

### Uptime and Error Investigation (y: 22)

| Panel | What It Shows | Why It Matters |
|-------|---------------|----------------|
| **Top Slow Routes p95 (ms, 5m)** | Table ranking routes by p95 latency (in milliseconds) over the last 5 minutes. Top 10 results. | Quick triage tool. During an incident, scan this table to find the worst-performing endpoints without manually filtering the timeseries charts. |
| **5xx Rate By Route (req/s)** | Per-route 5xx error rate timeseries with exemplar dots. | Identifies the error source. Click an exemplar dot on a spike to jump to the failing trace, then to its error logs. |

### Uptime and Logs (y: 30)

| Panel | What It Shows | Why It Matters |
|-------|---------------|----------------|
| **Uptime % By Instance (24h / 7d / 30d)** | Table showing per-instance uptime across three time windows using `avg_over_time(up{job="api-uptime"})`. | Identifies flapping or chronically unhealthy instances. An instance at 98% over 30d when others are at 100% needs investigation -- it may be on degraded hardware or experiencing OOM kills. |
| **Error Logs (Click trace_id to open Tempo trace)** | Loki logs panel showing entries where `StatusCode >= 500`. Query: `{service_name="observability-demo-api"} \| json \| attributes_StatusCode >= 500`. | The final piece of the investigation workflow. Each log line contains `trace_id` as a clickable link to Tempo. You see the error message, stack trace, tenant_id, and instance_id in structured JSON. |

### Exemplar Clickthrough (y: 37)

| Panel | What It Shows | Why It Matters |
|-------|---------------|----------------|
| **Exemplar Clickthrough (Raw Duration Buckets)** | Full-width timeseries of `rate(http_server_request_duration_seconds_bucket{le="+Inf"}[1m])` with exemplar dots enabled. Filtered to business and diagnostic routes. | This is the primary exemplar panel. Every dot represents a real request with a trace_id embedded as an exemplar label. Clicking a dot opens the trace in Tempo. This panel is intentionally placed after the error logs so that during investigation you can correlate: see the error log, then click an exemplar dot near the same timestamp to get the full distributed trace. |

### .NET Runtime Row (y: 44)

These panels cover the **Saturation** golden signal for application-level resources.

| Panel | Metric | Why It Matters |
|-------|--------|----------------|
| **GC Heap Size** | `dotnet_gc_heap_total_allocated_bytes` per instance | Memory pressure indicator. A steadily climbing line without plateaus suggests a memory leak. Sudden drops indicate GC collections. Large sawtooth patterns are normal for Gen 0/1 collection; a monotonically increasing line is not. |
| **Thread Pool Queue Length** | `dotnet_thread_pool_queue_length` per instance. Thresholds: green < 20, yellow 20-50, red > 50. | The earliest warning sign of thread starvation. When work items queue faster than the thread pool can process them, this value climbs. The `ThreadPoolSaturation` alert fires at 50. A sustained value above 20 means the service is struggling to keep up and latency will follow. |
| **Thread Pool Thread Count** | `dotnet_thread_pool_thread_count` per instance | Context for queue length. If thread count is maxed out and queue length is rising, the thread pool ceiling needs adjustment or the workload is too blocking (sync-over-async). If thread count is low but queue is high, the thread pool is still ramping up. |

### Database Connection Pool Row (y: 53)

These panels cover the **Saturation** golden signal for database-level resources.

| Panel | Metric(s) | Why It Matters |
|-------|-----------|----------------|
| **Connection Pool Usage (Idle / Used)** | `db_client_connections_idle` and `db_client_connections_used` per instance | Shows the ratio of available to active connections. A healthy pool has a buffer of idle connections. When idle drops to zero and used is at the pool maximum, new requests must wait. |
| **Pending Connection Requests** | `db_client_connections_pending_requests` per instance. Thresholds: green < 5, yellow 5-20, red > 20. | Requests waiting for a connection. Any non-zero value means the pool is exhausted and requests are queuing. Sustained values above 5 directly cause latency spikes visible in the latency panels above. |
| **Connection Create Time p95** | `histogram_quantile(0.95, rate(db_client_connections_create_time_bucket[5m]))` per instance | Measures how long it takes to establish new database connections. Spikes here indicate network issues between the API and PostgreSQL, DNS resolution delays, or PostgreSQL connection limits. Normal values are low single-digit milliseconds. |

---

## 4. Metric Reference Table

| Metric Name | Type | Source | Key Labels | Dashboard Panel(s) |
|-------------|------|--------|------------|-------------------|
| `http_server_request_duration_seconds` | Histogram (bucket/count/sum) | OpenTelemetry ASP.NET Core instrumentation | `exported_job`, `exported_instance`, `http_route`, `http_request_method`, `http_response_status_code`, `le` | Request Rate, Error Rate %, p95 Latency, Availability %, Latency Percentiles, Request Rate by Route, Error Rate by Route, Burn Rate gauges (via recording rules), Budget Remaining (via recording rules), Heatmap, all SLI stats, p95 By Route, Slow Requests, 5xx Rate, Top Slow Routes, Exemplar Clickthrough |
| `dotnet_gc_heap_total_allocated_bytes` | Gauge | OpenTelemetry .NET Runtime instrumentation | `exported_job`, `exported_instance` | GC Heap Size |
| `dotnet_thread_pool_queue_length` | Gauge | OpenTelemetry .NET Runtime instrumentation | `exported_job`, `exported_instance` | Thread Pool Queue Length |
| `dotnet_thread_pool_thread_count` | Gauge | OpenTelemetry .NET Runtime instrumentation | `exported_job`, `exported_instance` | Thread Pool Thread Count |
| `db_client_connections_idle` | Gauge | Npgsql OpenTelemetry instrumentation | `exported_job`, `exported_instance` | Connection Pool Usage (Idle / Used) |
| `db_client_connections_used` | Gauge | Npgsql OpenTelemetry instrumentation | `exported_job`, `exported_instance` | Connection Pool Usage (Idle / Used) |
| `db_client_connections_pending_requests` | Gauge | Npgsql OpenTelemetry instrumentation | `exported_job`, `exported_instance` | Pending Connection Requests |
| `db_client_connections_create_time` | Histogram (bucket/count/sum) | Npgsql OpenTelemetry instrumentation | `exported_job`, `exported_instance`, `le` | Connection Create Time p95 |
| `up` | Gauge | Prometheus scrape target health | `job`, `instance` | Uptime % (24h), Uptime % By Instance (24h/7d/30d), ApiInstanceDown alert |

### Recording Rules

These pre-computed metrics power the SLO panels and alerts. All are evaluated every 15 seconds (except budget rules at 60s).

**Group: `sli_preaggregation`**

| Rule | Expression Summary | Used By |
|------|--------------------|---------|
| `job:http_server_requests:rate5m` | Total service request rate | Denominator in error ratio |
| `job:http_server_errors:rate5m` | Total 5xx request rate | Numerator in error ratio |
| `job:http_server_error_ratio:rate5m` | Error rate as a fraction (0-1) | HighErrorRate alert |
| `job:http_server_latency_sli:rate5m` | Fraction of requests within 500ms (excluding `/diagnostics/slow`) | Latency SLI monitoring |
| `job_route:http_server_requests:rate5m` | Per-route request rate | Route-level analysis |
| `job_route:http_server_errors:rate5m` | Per-route 5xx rate | Route-level error analysis |
| `job_instance:http_server_requests:rate5m` | Per-instance request rate | Instance-level analysis |
| `job_instance:http_server_errors:rate5m` | Per-instance 5xx rate | Instance-level error analysis |

**Group: `slo_error_burn_rate`**

| Rule | Window | Used By |
|------|--------|---------|
| `job:http_server_availability_burn_rate:5m` | 5 min | AvailabilityBurnRateCritical alert (short window) |
| `job:http_server_availability_burn_rate:30m` | 30 min | AvailabilityBurnRateHigh alert (short window) |
| `job:http_server_availability_burn_rate:1h` | 1 hour | Availability Burn Rate gauge, Burn Rate Over Time chart, AvailabilityBurnRateCritical alert (long window) |
| `job:http_server_availability_burn_rate:6h` | 6 hours | AvailabilityBurnRateHigh alert (long window), AvailabilityBurnRateElevated alert |

**Group: `slo_latency_burn_rate`**

| Rule | Window | Used By |
|------|--------|---------|
| `job:http_server_latency_burn_rate:5m` | 5 min | LatencyBurnRateCritical alert (short window) |
| `job:http_server_latency_burn_rate:30m` | 30 min | LatencyBurnRateHigh alert (short window) |
| `job:http_server_latency_burn_rate:1h` | 1 hour | Latency Burn Rate gauge, Burn Rate Over Time chart, LatencyBurnRateCritical alert (long window) |
| `job:http_server_latency_burn_rate:6h` | 6 hours | LatencyBurnRateHigh alert (long window), LatencyBurnRateElevated alert |

**Group: `slo_error_budget`** (60s interval)

| Rule | Window | Used By |
|------|--------|---------|
| `job:http_server_availability_budget_remaining_pct:30d` | 30 days | Availability Budget Remaining stat panel |
| `job:http_server_latency_budget_remaining_pct:30d` | 30 days | Latency Budget Remaining stat panel |

---

## 5. End-to-End Exemplar Workflow

Exemplars are the bridge between aggregate metrics and individual request traces. This project uses them to enable a complete investigation path: **metric chart --> distributed trace --> correlated logs**.

### How Exemplars Get Created

1. The .NET API emits `http_server_request_duration_seconds` histogram observations via OpenTelemetry.
2. When a request is part of a sampled trace, the OpenTelemetry SDK attaches the `trace_id` and `span_id` as exemplar labels on the histogram data point.
3. The histogram is exported via OTLP to the OpenTelemetry Collector, which forwards it to Prometheus (via the Prometheus remote write exporter on port 8889).
4. Prometheus stores the exemplar alongside the histogram bucket, retaining it with the time-series data.
5. Grafana's Prometheus data source has exemplar support enabled. When a panel has `"exemplar": true`, Grafana queries both the time-series data and associated exemplars.

### How to Use Exemplars in Practice

**Step 1: Spot the anomaly on a metric chart.**

On either dashboard, look at any timeseries panel with exemplar dots enabled (Latency Percentiles, Request Rate by Route, Error Rate by Route, p95 By Route, Slow Requests, 5xx Rate, or Exemplar Clickthrough). Exemplar dots appear as small diamond-shaped markers overlaid on the line chart.

**Step 2: Click the exemplar dot.**

Hovering over a dot shows the `trace_id` and the recorded value. Clicking it opens the linked trace in Tempo. Grafana resolves the `trace_id` through the configured Tempo data source.

**Step 3: Read the trace in Tempo.**

The trace view shows the full request lifecycle:
- HTTP handler span (ASP.NET Core instrumentation)
- Application service span (custom spans in `WorkItemService`)
- Database spans (Npgsql instrumentation)
- Span attributes include `tenant_id`, `http.route`, `http.status_code`, `db.statement`

Look for spans with error status, unusually long durations, or gaps between spans (indicating queuing).

**Step 4: Jump to correlated logs in Loki.**

From the Tempo trace view, use "Logs for this span" or manually query Loki with the trace_id:

```logql
{service_name="observability-demo-api"} | json | trace_id = "<trace-id-from-tempo>"
```

The structured JSON logs include `trace_id`, `span_id`, `tenant_id`, `instance_id`, and the full log message with any exception details.

**Step 5: Alternatively, start from the Error Logs panel.**

The "Error Logs" panel on the Reliability And Drilldown dashboard shows all 5xx log entries. Each entry contains a `trace_id` field that links to Tempo. This is useful when you want to start from the error and work backward to understand timing, rather than starting from a metric spike.

### The Full Loop

```
Metric spike on chart
    |
    +--> Click exemplar dot
            |
            +--> Tempo trace: see which span is slow/failing
                    |
                    +--> Loki logs: see error message, stack trace, tenant context
                            |
                            +--> Root cause identified
```

---

## 6. SLO Framework

### Targets

| SLO | Target | Error Budget (30-day) | Meaning |
|-----|--------|-----------------------|---------|
| **Availability** | 99.9% | 0.1% of requests can be 5xx | ~43 minutes of total downtime equivalent per 30-day window (at uniform traffic) |
| **Latency** | 99% within 500ms | 1% of requests can exceed 500ms | 1 in 100 requests is allowed to be slow |

### Error Budgets Explained

An error budget is the inverse of the SLO target. It quantifies how much unreliability is acceptable.

- **Availability budget:** If the service handles 1,000,000 requests in 30 days, the budget allows 1,000 failed requests (0.1%).
- **Latency budget:** Of those 1,000,000 requests, 10,000 (1%) can exceed 500ms.

The `job:http_server_availability_budget_remaining_pct:30d` and `job:http_server_latency_budget_remaining_pct:30d` recording rules track how much budget remains as a percentage:
- **100%** = no budget consumed (zero errors/slow requests)
- **75%** = 25% of budget used (healthy)
- **25%** = 75% of budget used (caution -- defer risky changes)
- **0%** = budget exhausted (SLO violated for the period)
- **Negative** = overspent (SLO violated and continuing to degrade)

### Multi-Window Burn Rate Model

Burn rate measures how fast the error budget is being consumed relative to the sustainable rate.

```
burn_rate = actual_error_rate / error_budget_rate
```

A burn rate of **1.0x** means you are consuming budget at exactly the pace that would exhaust it in 30 days. Higher values mean faster consumption.

| Burn Rate | Budget Exhaustion Time | Interpretation |
|-----------|----------------------|----------------|
| 1x | 30 days | Sustainable (barely) |
| 6x | 5 days | Significant degradation |
| 14.4x | ~50 hours | Severe -- budget gone in 2 days |
| 50x | ~14 hours | Major outage in progress |
| 720x | 1 hour | Complete failure |

### Why Multiple Windows?

A single-window burn rate is either too noisy (short window catches transient spikes) or too slow (long window misses fast-moving incidents). The multi-window approach requires both a short and long window to exceed the threshold before alerting:

| Alert Tier | Short Window | Long Window | Budget Consumed | Detection Time |
|------------|-------------|-------------|-----------------|----------------|
| Critical (14.4x) | 5 min | 1 hour | 2% in 1 hour | ~2 minutes |
| High (6x) | 30 min | 6 hours | 5% in 6 hours | ~5 minutes |
| Elevated (1x) | -- | 6 hours | >sustainable rate | ~30 minutes |

This design prevents pages from transient spikes (the long window acts as confirmation) while still catching real incidents quickly (the short window provides fast detection).

---

## 7. Alert Severity Model and Response Playbook

### Alert Groups Overview

**Group: `slo_burn_rate_alerts`** -- SLO-based alerts using the multi-window burn rate model.

| Alert | Threshold | `for` Duration | Severity | Meaning |
|-------|-----------|----------------|----------|---------|
| `AvailabilityBurnRateCritical` | 5m > 14.4x AND 1h > 14.4x | 2 min | **page** | Burning 2% of 30-day availability budget per hour. Active incident. |
| `AvailabilityBurnRateHigh` | 30m > 6x AND 6h > 6x | 5 min | **page** | Burning 5% of budget per 6 hours. Sustained degradation requiring immediate attention. |
| `AvailabilityBurnRateElevated` | 6h > 1x | 30 min | **ticket** | Budget consumption exceeds sustainable rate. Will violate SLO if not addressed. |
| `LatencyBurnRateCritical` | 5m > 14.4x AND 1h > 14.4x | 2 min | **page** | Rapid latency degradation. Most requests exceeding 500ms. |
| `LatencyBurnRateHigh` | 30m > 6x AND 6h > 6x | 5 min | **page** | Sustained latency degradation requiring immediate attention. |
| `LatencyBurnRateElevated` | 6h > 1x | 30 min | **ticket** | Latency budget consumption unsustainable. Investigation needed. |

**Group: `infrastructure_alerts`** -- Symptom-based alerts for infrastructure-level issues.

| Alert | Threshold | `for` Duration | Severity | Meaning |
|-------|-----------|----------------|----------|---------|
| `HighErrorRate` | Error ratio > 5% | 3 min | **warning** | More than 1 in 20 requests failing. |
| `HighP99Latency` | p99 > 2 seconds | 5 min | **warning** | Tail latency severely degraded. |
| `ApiInstanceDown` | `up{job="api-uptime"} == 0` | 1 min | **page** | An API instance is unreachable. Prometheus cannot scrape its health endpoint. |
| `ThreadPoolSaturation` | Queue length > 50 | 3 min | **warning** | Thread pool is saturated. Requests are queuing, latency will increase. |

### Severity Definitions

| Severity | Response | Channel | SLA |
|----------|----------|---------|-----|
| **page** | Wake someone up. Drop everything and investigate immediately. | PagerDuty / on-call rotation | Acknowledge within 5 minutes, start mitigation within 15 minutes |
| **ticket** | Create a work item. Investigate during business hours. | Ticketing system (Jira, Linear, etc.) | Triage within 4 hours, resolve within 2 business days |
| **warning** | Informational. Monitor for escalation. | Slack/Teams alert channel | Review during next business day |

### Response Playbook

#### AvailabilityBurnRateCritical / AvailabilityBurnRateHigh (page)

1. **Open the SRE Overview dashboard.** Check the Error Rate % stat and the Error Rate by Route panel to identify which routes are failing.
2. **Switch to Reliability And Drilldown.** Check the 5xx Rate By Route panel for route-level isolation.
3. **Click an exemplar dot** on the 5xx rate spike to open the failing trace in Tempo.
4. **Read the trace.** Look for error spans -- database timeout? Unhandled exception? Upstream dependency failure?
5. **Check error logs.** Use the Error Logs panel or query Loki with the trace_id for full stack traces.
6. **Check infrastructure panels.** Is the thread pool saturated? Are DB connections exhausted? Is an instance down?
7. **Mitigate.** Common actions: restart failing instance, scale horizontally (`podman compose up -d --scale api=N`), roll back recent deployment, circuit-break a failing dependency.

#### LatencyBurnRateCritical / LatencyBurnRateHigh (page)

1. **Open the SRE Overview dashboard.** Check p95 Latency stat and the Latency Percentiles timeseries.
2. **Check the heatmap.** Is the distribution bimodal (some fast, some slow) or has the entire distribution shifted?
3. **Switch to Reliability And Drilldown.** Check p95 Latency By Route and Slow Requests > 500ms By Route.
4. **Click an exemplar** on the slow-request panel to open a slow trace in Tempo. Look for the bottleneck span.
5. **Check saturation panels.** Thread Pool Queue Length rising? DB Connection Create Time spiking? GC Heap growing unbounded?
6. **Mitigate.** Common actions: scale horizontally to distribute load, increase connection pool size, identify and optimize slow queries, check for sync-over-async patterns, increase thread pool min threads.

#### AvailabilityBurnRateElevated / LatencyBurnRateElevated (ticket)

1. **Do not panic.** This is a slow burn, not an emergency.
2. **Check the Budget Remaining panels.** If above 50%, you have time. If below 25%, prioritize.
3. **Review the Burn Rate Over Time chart.** Is the burn rate trending up, stable, or decreasing?
4. **Identify the source.** Use per-route panels to find which endpoint is contributing most to budget consumption.
5. **Create a ticket** to investigate and fix the underlying cause (slow query, noisy retry loop, resource leak).
6. **Consider a change freeze** if budget is below 25% to avoid further risk.

#### HighErrorRate (warning)

1. **Check if an SLO burn rate alert is also firing.** If so, follow that playbook instead -- it is more actionable.
2. **Identify the error source** using Error Rate by Route on the SRE Overview.
3. **Check if errors are transient** (spike and recovery) or sustained (flat elevated line).
4. **If transient:** Monitor for recurrence. Check recent deployments.
5. **If sustained:** Investigate per the availability burn rate playbook.

#### HighP99Latency (warning)

1. **Check the heatmap** for distribution shape.
2. **Identify affected routes** via p95 By Route on Reliability And Drilldown.
3. **Click exemplar dots** on slow requests to get representative traces.
4. **Check DB connection pool panels** -- pending requests and create time often explain latency tails.

#### ApiInstanceDown (page)

1. **Check Uptime % By Instance table** on Reliability And Drilldown. Which instance(s) are down?
2. **Check container status:** `podman compose -f ops/compose/compose.yaml ps`
3. **Check container logs:** `podman compose -f ops/compose/compose.yaml logs api`
4. **If OOM killed:** Check GC Heap Size for the period before the crash. Consider increasing memory limits.
5. **If crash loop:** Check application logs in Loki for startup exceptions.
6. **Restart the instance** or scale to replace it: `podman compose -f ops/compose/compose.yaml up -d --scale api=N`

#### ThreadPoolSaturation (warning)

1. **Check Thread Pool Queue Length** on Reliability And Drilldown. Is it rising or stable at an elevated level?
2. **Check Thread Pool Thread Count.** If at maximum and queue is rising, the workload exceeds capacity.
3. **Check for sync-over-async patterns** in traces -- long gaps between spans in a single thread suggest blocking calls.
4. **Check DB connection pool.** If pending requests > 0, threads may be blocked waiting for connections.
5. **Mitigate:** Scale horizontally or investigate blocking code paths.
