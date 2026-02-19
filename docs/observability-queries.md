# Observability Queries

## Latency Percentiles
Service-level:
```promql
histogram_quantile(0.50, sum by (le) (rate(http_server_request_duration_seconds_bucket{job="observability-demo-api"}[5m]))) * 1000
histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket{job="observability-demo-api"}[5m]))) * 1000
histogram_quantile(0.99, sum by (le) (rate(http_server_request_duration_seconds_bucket{job="observability-demo-api"}[5m]))) * 1000
```

Route-level:
```promql
histogram_quantile(0.95, sum by (http_route, le) (rate(http_server_request_duration_seconds_bucket{job="observability-demo-api"}[5m]))) * 1000
```

## SLI/SLO Queries
Availability SLI:
```promql
100 * (sum(increase(http_server_request_duration_seconds_count{job="observability-demo-api",http_response_status_code!~"5.."}[30d])) / sum(increase(http_server_request_duration_seconds_count{job="observability-demo-api"}[30d])))
```

Error-rate SLI:
```promql
100 * (sum(increase(http_server_request_duration_seconds_count{job="observability-demo-api",http_response_status_code=~"5.."}[30d])) / sum(increase(http_server_request_duration_seconds_count{job="observability-demo-api"}[30d])))
```

Latency SLI (requests <= 500ms, excluding intentional slow endpoint):
```promql
100 * (sum(increase(http_server_request_duration_seconds_bucket{job="observability-demo-api",http_route!="diagnostics/slow",le="0.5"}[30d])) / sum(increase(http_server_request_duration_seconds_count{job="observability-demo-api",http_route!="diagnostics/slow"}[30d])))
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
sum by (http_route) (rate(http_server_request_duration_seconds_count{job="observability-demo-api"}[5m]))
-
sum by (http_route) (rate(http_server_request_duration_seconds_bucket{job="observability-demo-api",le="0.5"}[5m]))
```

## Trace + Log Pivot
1. Search Tempo with `service.name=observability-demo-api`.
2. Open a slow trace and copy `trace_id` if needed.
3. Query Loki:
```logql
{service_name="observability-demo-api"} | json | trace_id="<trace_id>"
```
