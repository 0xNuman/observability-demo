# Observability Queries

## Latency Percentiles
Service-level:
```promql
histogram_quantile(0.50, sum by (le) (rate(http_server_request_duration_seconds_bucket{exported_job="observability-demo-api"}[5m]))) * 1000
histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{exported_job="observability-demo-api"}[5m]))) * 1000
histogram_quantile(0.99, sum by (le) (rate(http_server_request_duration_seconds_bucket{exported_job="observability-demo-api"}[5m]))) * 1000
```

Route-level:
```promql
histogram_quantile(0.95, sum by (http_route, le) (rate(http_server_request_duration_seconds_bucket{exported_job="observability-demo-api"}[5m]))) * 1000
```

## SLI/SLO Queries
Availability SLI:
```promql
100 * (sum(increase(http_server_request_duration_seconds_count{exported_job="observability-demo-api",http_response_status_code!~"5.."}[30d])) / sum(increase(http_server_request_duration_seconds_count{exported_job="observability-demo-api"}[30d])))
```

Error-rate SLI:
```promql
100 * (sum(increase(http_server_request_duration_seconds_count{exported_job="observability-demo-api",http_response_status_code=~"5.."}[30d])) / sum(increase(http_server_request_duration_seconds_count{exported_job="observability-demo-api"}[30d])))
```

Latency SLI (requests <= 500ms, excluding intentional slow endpoint):
```promql
100 * (sum(increase(http_server_request_duration_seconds_bucket{exported_job="observability-demo-api",http_route!="diagnostics/slow",le="0.5"}[30d])) / sum(increase(http_server_request_duration_seconds_count{exported_job="observability-demo-api",http_route!="diagnostics/slow"}[30d])))
```

## Uptime Queries
Current:
```promql
100 * avg(up{job="api-uptime"})
```

By instance:
```promql
100 * avg_over_time(up{job="api-uptime"}[24h])
100 * avg_over_time(up{job="api-uptime"}[7d])
100 * avg_over_time(up{job="api-uptime"}[30d])
```

## Slow Request Discovery
Slow requests (>500ms) by route:
```promql
sum by (http_route) (rate(http_server_request_duration_seconds_count{exported_job="observability-demo-api"}[5m]))
-
sum by (http_route) (rate(http_server_request_duration_seconds_bucket{exported_job="observability-demo-api",le="0.5"}[5m]))
```

## Trace + Log Pivot
1. Search Tempo with `service.name=observability-demo-api`.
2. Open a slow trace and copy `traceid` if needed.
3. Query Loki:
```logql
{service_name="observability-demo-api"} | json | traceid="<traceid>"
```

Error-only logs:
```logql
{service_name="observability-demo-api"} | json | attributes_StatusCode >= 500
```

## Recording Rules Reference

Pre-computed recording rules avoid expensive on-the-fly aggregation in dashboards. Defined in `ops/prometheus/recording-rules.yml`.

### SLI Pre-aggregation Rules (group: `sli_preaggregation`, 15s interval)

| Rule Name | Description |
|-----------|-------------|
| `job:http_server_requests:rate5m` | Service-level request rate (req/s) |
| `job:http_server_errors:rate5m` | Service-level 5xx error rate (req/s) |
| `job:http_server_error_ratio:rate5m` | Fraction of requests returning 5xx |
| `job:http_server_latency_sli:rate5m` | Fraction of requests served within 500ms |
| `job_route:http_server_requests:rate5m` | Per-route request rate |
| `job_route:http_server_errors:rate5m` | Per-route 5xx error rate |
| `job_instance:http_server_requests:rate5m` | Per-instance request rate |
| `job_instance:http_server_errors:rate5m` | Per-instance 5xx error rate |

### Availability Burn Rate Rules (group: `slo_error_burn_rate`, 15s interval)

SLO: **99.9%** availability (error budget = 0.001)

| Rule Name | Window | Burn Rate = 1 means... |
|-----------|--------|----------------------|
| `job:http_server_availability_burn_rate:5m` | 5 min | Consuming budget at exactly the sustainable rate |
| `job:http_server_availability_burn_rate:30m` | 30 min | Same, smoothed over 30 minutes |
| `job:http_server_availability_burn_rate:1h` | 1 hour | Same, smoothed over 1 hour |
| `job:http_server_availability_burn_rate:6h` | 6 hours | Same, smoothed over 6 hours |

### Latency Burn Rate Rules (group: `slo_latency_burn_rate`, 15s interval)

SLO: **99%** of requests within 500ms (error budget = 0.01)

| Rule Name | Window |
|-----------|--------|
| `job:http_server_latency_burn_rate:5m` | 5 min |
| `job:http_server_latency_burn_rate:30m` | 30 min |
| `job:http_server_latency_burn_rate:1h` | 1 hour |
| `job:http_server_latency_burn_rate:6h` | 6 hours |

### Error Budget Rules (group: `slo_error_budget`, 60s interval)

| Rule Name | Description |
|-----------|-------------|
| `job:http_server_availability_budget_remaining_pct:30d` | % of 30-day availability budget remaining (100% = full, 0% = exhausted) |
| `job:http_server_latency_budget_remaining_pct:30d` | % of 30-day latency budget remaining |

## Interpreting Burn Rates

Burn rate measures how fast you are consuming your error budget relative to the sustainable rate over a 30-day window.

| Burn Rate | Meaning | Budget Impact |
|-----------|---------|---------------|
| **0** | No errors / all requests within SLO | Budget untouched |
| **1** | Consuming at exactly the sustainable rate | Budget exhausts at exactly 30 days |
| **6** | 6x the sustainable rate | Budget exhausts in 5 days |
| **14.4** | 14.4x the sustainable rate | Budget exhausts in ~2 days |
| **> 14.4** | Severe degradation | Budget exhausts in hours |

### Alert Thresholds (Multi-Window)

Alerts use two windows simultaneously to reduce false positives:

| Severity | Short Window | Long Window | Budget Consumed |
|----------|-------------|-------------|-----------------|
| **Page** (critical) | 5m > 14.4x | 1h > 14.4x | 2% in 1 hour |
| **Page** (high) | 30m > 6x | 6h > 6x | 5% in 6 hours |
| **Ticket** (elevated) | — | 6h > 1x | 10% in 3 days |

The short window catches fast spikes; the long window confirms the problem is sustained. Both must fire simultaneously to trigger the alert.
