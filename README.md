# .NET 10 Observability Demo

A production-ready demonstration of comprehensive observability for .NET 10 WebAPI applications using OpenTelemetry, showcasing metrics, traces, and logs with full correlation.

## 🎯 What This Demo Shows

- **Complete observability stack** with OpenTelemetry Collector as the central pipeline
- **Automatic instrumentation** for ASP.NET Core and HttpClient
- **Custom business metrics** alongside automatic instrumentation
- **Exemplars** - click a metric spike to jump directly to the trace that caused it
- **Distributed tracing** with external API calls
- **Log-to-trace correlation** via trace IDs
- **Runtime metrics** including GC, CPU, memory, and thread pool

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              GRAFANA                                    │
│                    (Unified Visualization)                              │
│         ┌──────────────────┬──────────────────┬─────────────────┐       │
│         │    Metrics       │     Traces       │      Logs       │       │
│         │   Dashboard      │    Explorer      │    Explorer     │       │
│         └────────┬─────────┴────────┬─────────┴────────┬────────┘       │
│                  │                  │                  │                │
│              exemplar           trace-to-log      trace-to-metric       │
│                link               link               link               │
└──────────────────┼──────────────────┼──────────────────┼────────────────┘
                   │                  │                  │
         ┌─────────▼─────────┐ ┌──────▼──────┐ ┌─────────▼───────┐
         │    PROMETHEUS     │ │    TEMPO    │ │      LOKI       │
         │  (Metrics Store)  │ │  (Traces)   │ │     (Logs)      │
         │                   │ │             │ │                 │
         │  • Exemplars      │ │ • Span      │ │ • Structured    │
         │  • Native Hists   │ │   Metrics   │ │   Logs          │
         └─────────▲─────────┘ └──────▲──────┘ └────────▲────────┘
                   │                  │                 │
                   │    ┌─────────────┴────────────┐    │
                   │    │                          │    │
         ┌─────────┴────┴──────────────────────────┴────┴─────────┐
         │               OPENTELEMETRY COLLECTOR                  │
         │                                                        │
         │  Receivers:  OTLP (gRPC:4317, HTTP:4318)               │
         │  Processors: batch, resource, memory_limiter           │
         │  Exporters:  prometheus, otlp/tempo, loki              │
         └────────────────────────▲───────────────────────────────┘
                                  │
                                  │ OTLP (traces, metrics, logs)
                                  │
         ┌────────────────────────┴────────────────────────────────┐
         │                 .NET 10 WebAPI                          │
         │                                                         │
         │  ┌─────────────────────────────────────────────────┐    │
         │  │              OpenTelemetry SDK                  │    │
         │  │                                                 │    │
         │  │  • ASP.NET Core Instrumentation                 │    │
         │  │  • HttpClient Instrumentation                   │    │
         │  │  • Runtime Instrumentation                      │    │
         │  │  • Custom Metrics (DemoTelemetry)               │    │
         │  │  • OTLP Exporters                               │    │
         │  └─────────────────────────────────────────────────┘    │
         │                                                         │
         │  Endpoints:                                             │
         │  • GET /api/demo/hello     → Baseline metrics           │
         │  • GET /api/demo/users/{id} → External API + tracing    │
         │  • GET /api/demo/posts      → External API + tracing    │
         │  • GET /api/demo/slow       → Latency testing           │
         │  • GET /api/demo/error      → Error tracking            │
         └────────────────────────────┬────────────────────────────┘
                                      │
                                      ▼
                        ┌─────────────────────────┐
                        │   JSONPlaceholder API   │
                        │  (External Dependency)  │
                        └─────────────────────────┘
```

## 🚀 Quick Start

```bash
# Start everything
docker-compose up -d --build

# Generate traffic
./scripts/generate-traffic.sh

# Open Grafana
open http://localhost:3000  # admin/admin
```

See [QUICKSTART.md](QUICKSTART.md) for detailed instructions.

## 📊 Metrics Collected

### Automatic Instrumentation

| Metric | Type | Description |
|--------|------|-------------|
| `http_server_request_duration_seconds` | Histogram | Server-side request duration (TTFB) |
| `http_client_request_duration_seconds` | Histogram | External HTTP call duration |
| `process_cpu_time_seconds` | Counter | CPU time used |
| `dotnet_gc_heap_size_bytes` | Gauge | GC heap size by generation |
| `dotnet_gc_collections_total` | Counter | GC collection count |
| `dotnet_thread_pool_thread_count` | Gauge | Thread pool thread count |
| `dotnet_thread_pool_queue_length` | Gauge | Thread pool work queue length |

### Custom Business Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `demo.requests.total` | Counter | endpoint, status_code, status_class | Request count by endpoint |
| `demo.request.duration` | Histogram | endpoint, status_code, status_class | Request duration in ms |
| `demo.external_api_calls.total` | Counter | api, endpoint, success | External API call count |

## 🔗 Exemplar Flow

Exemplars are the "magic" feature that links metrics to traces:

1. **Application**: Records metrics with trace context via `ExemplarFilterType.TraceBased`
2. **OTel Collector**: Preserves exemplars in Prometheus export
3. **Prometheus**: Stores exemplars with `--enable-feature=exemplar-storage`
4. **Tempo**: Generates span metrics with exemplars via `metrics_generator`
5. **Grafana**: Displays exemplars as clickable dots on time series

### Demo: Click Metric → View Trace

1. Generate a slow request:
   ```bash
   curl "http://localhost:8080/api/demo/slow?delayMs=2000"
   ```
2. In Grafana, open the "Request Duration Percentiles" panel
3. Look for the spike at p99
4. Click the small square (exemplar) on the spike
5. You're now viewing the exact trace for that slow request!

## 📈 Dashboard Panels

The pre-configured dashboard includes:

### RED Metrics
- **Request Rate by Endpoint** - Line chart showing requests/second per endpoint
- **Error Rate** - Gauge showing percentage of 5xx responses
- **Total Request Rate** - Aggregate request throughput

### Latency Analysis
- **Request Duration Percentiles** - p50, p95, p99 with exemplars
- **HTTP Server Duration (TTFB)** - Server processing time by route

### External Dependencies
- **External API Call Rate** - Calls to jsonplaceholder by endpoint
- **HTTP Client Duration** - External API latency

### Resource Utilization
- **CPU Utilization** - Process CPU percentage
- **GC Heap Size** - Memory by generation (Gen0, Gen1, Gen2, LOH, POH)
- **GC Collection Rate** - Collections per second by generation
- **Thread Pool Metrics** - Thread count and queue length
- **GC Pause Ratio** - Time spent in GC pauses

## 🧪 Testing Scenarios

### Scenario 1: Baseline Performance
```bash
# Generate steady traffic
RATE=medium DURATION=120 ./scripts/generate-traffic.sh
```
Expected: Stable metrics across all panels

### Scenario 2: Latency Spike
```bash
# Generate very slow requests
for i in {1..10}; do
  curl "http://localhost:8080/api/demo/slow?delayMs=3000" &
done
wait
```
Expected: p99 spike visible, click exemplar to see trace

### Scenario 3: Error Rate Increase
```bash
# Generate errors
for i in {1..50}; do
  curl "http://localhost:8080/api/demo/error"
done
```
Expected: Error rate gauge increases, error traces visible

### Scenario 4: External API Dependency
```bash
# High volume external calls
for i in {1..100}; do
  curl "http://localhost:8080/api/demo/users/$((RANDOM % 10 + 1))" &
done
wait
```
Expected: External API metrics increase, distributed traces show child spans

## 🔧 Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://otel-collector:4317` | OTel Collector endpoint |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Environment name |

### Grafana Credentials

- **Username**: admin
- **Password**: admin

## 📁 Project Structure

```
observability-demo/
├── src/
│   └── ObservabilityDemo/
│       ├── Program.cs              # App configuration + OTel setup
│       ├── Endpoints/
│       │   └── DemoEndpoints.cs    # API endpoints
│       ├── Services/
│       │   ├── IExternalApiService.cs
│       │   └── ExternalApiService.cs
│       ├── Telemetry/
│       │   └── DemoTelemetry.cs    # Custom metrics + activities
│       └── Dockerfile
├── config/
│   ├── otel-collector-config.yaml  # Collector pipeline
│   ├── tempo-config.yaml           # Tracing backend
│   ├── prometheus.yml              # Metrics storage
│   ├── loki-config.yaml            # Log aggregation
│   └── grafana/
│       ├── provisioning/
│       │   ├── datasources/        # Auto-configured datasources
│       │   └── dashboards/         # Dashboard provisioning
│       └── dashboards/
│           └── observability-demo.json
├── scripts/
│   └── generate-traffic.sh         # Traffic generator
├── docker-compose.yml
├── README.md
├── QUICKSTART.md
└── CONCEPTS.md
```

## 🔍 Troubleshooting

### No Metrics in Grafana

1. Check OTel Collector is receiving data:
   ```bash
   docker-compose logs otel-collector
   ```
2. Verify Prometheus is scraping:
   - Open http://localhost:9090/targets
   - Check `otel-collector` target is UP

### No Traces in Tempo

1. Check Tempo is healthy:
   ```bash
   curl http://localhost:3200/ready
   ```
2. Verify traces are being sent:
   ```bash
   docker-compose logs otel-collector | grep -i trace
   ```

### Exemplars Not Showing

1. Ensure feature flag is enabled in Prometheus
2. Wait 1-2 minutes for data to flow
3. Check "Request Duration Percentiles" panel specifically

### Application Not Starting

1. Check Docker logs:
   ```bash
   docker-compose logs webapi
   ```
2. Verify OTel Collector is healthy first:
   ```bash
   docker-compose ps
   ```

## 📚 Learn More

- [CONCEPTS.md](CONCEPTS.md) - Observability theory (percentiles, RED method, SLOs)
- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)
- [Grafana Tempo Documentation](https://grafana.com/docs/tempo/latest/)
- [Prometheus Exemplars](https://prometheus.io/docs/prometheus/latest/feature_flags/#exemplars-storage)

## 🤝 Contributing

This is a demonstration project. Feel free to fork and adapt for your own learning or production use.

## 📄 License

MIT License - use freely for learning and production.
