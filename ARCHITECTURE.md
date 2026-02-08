# Observability Demo Architecture

This document explains the architecture and data flow of the observability demo stack.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              YOUR APPLICATION                               │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                     .NET 10 WebAPI (port 8080)                      │    │
│  │                                                                     │    │
│  │  ┌──────────────────────────────────────────────────────────────┐   │    │
│  │  │              OpenTelemetry SDK (Auto-Instrumentation)        │   │    │
│  │  │  • ASP.NET Core Instrumentation (HTTP server metrics/traces) │   │    │
│  │  │  • HttpClient Instrumentation (outgoing HTTP metrics/traces) │   │    │
│  │  │  • Runtime Instrumentation (CPU, memory, GC, threads)        │   │    │
│  │  │  • OTLP Exporter (sends all telemetry to collector)          │   │    │
│  │  └──────────────────────────────────────────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                        │
│                                    │ OTLP (gRPC :4317)                      │
│                                    ▼                                        │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         OPENTELEMETRY COLLECTOR                             │
│                              (port 4317/4318)                               │
│                                                                             │
│  Receives all telemetry data and routes it to appropriate backends:         │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐                      │
│  │   Traces    │    │   Metrics   │    │    Logs     │                      │
│  └──────┬──────┘    └──────┬──────┘    └──────┬──────┘                      │
│         │                  │                  │                             │
└─────────┼──────────────────┼──────────────────┼─────────────────────────────┘
          │                  │                  │
          ▼                  ▼                  ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│     TEMPO       │ │   PROMETHEUS    │ │      LOKI       │
│  (port 3200)    │ │   (port 9090)   │ │   (port 3100)   │
│                 │ │                 │ │                 │
│  Distributed    │ │  Time-series    │ │  Log            │
│  Tracing        │ │  Metrics DB     │ │  Aggregation    │
│  Backend        │ │                 │ │                 │
└────────┬────────┘ └────────┬────────┘ └────────┬────────┘
         │                   │                   │
         └───────────────────┼───────────────────┘
                             │
                             ▼
                  ┌─────────────────────┐
                  │       GRAFANA       │
                  │     (port 3000)     │
                  │                     │
                  │   Visualization &   │
                  │   Unified UI for    │
                  │   all telemetry     │
                  └─────────────────────┘
```

## Services Explained

### 1. WebAPI (.NET 10 Application)

**Container:** `observability-demo-webapi`
**Port:** `8080`
**Purpose:** Your application that generates telemetry data

The WebAPI is a .NET 10 application with OpenTelemetry SDK configured via a single extension method (`builder.AddObservability()`). It uses **automatic instrumentation only** - no manual telemetry code in business logic.

**What it produces:**
- **Traces:** Distributed traces for every HTTP request (server and client)
- **Metrics:** Request duration histograms, request counts, .NET runtime metrics
- **Logs:** Structured logs with trace correlation (TraceId, SpanId)

**Environment Variables:**

| Variable | Purpose |
|----------|---------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Where to send telemetry (collector) |
| `OTEL_SERVICE_NAME` | Service name for identification |
| `OTEL_RESOURCE_ATTRIBUTES` | Additional resource attributes |

---

### 2. OpenTelemetry Collector

**Container:** `otel-collector`
**Image:** `otel/opentelemetry-collector-contrib:0.115.1`
**Ports:**
- `4317` - OTLP gRPC receiver
- `4318` - OTLP HTTP receiver
- `8889` - Prometheus metrics endpoint (for scraping)
- `13133` - Health check endpoint

**Purpose:** Central hub that receives, processes, and exports telemetry data

The collector acts as a **telemetry pipeline** with three main components:

```yaml
# Receivers - Accept telemetry from applications
receivers:
  otlp:
    protocols:
      grpc: { endpoint: "0.0.0.0:4317" }
      http: { endpoint: "0.0.0.0:4318" }

# Processors - Transform/batch data
processors:
  batch: { timeout: 1s, send_batch_size: 1024 }

# Exporters - Send to backends
exporters:
  otlp/tempo:     # Traces → Tempo
  prometheusremotewrite:  # Metrics → Prometheus
  loki:           # Logs → Loki
```

**Why use a collector instead of direct export?**
- **Decoupling:** App doesn't need to know about backend infrastructure
- **Buffering:** Handles backpressure and retries
- **Processing:** Can filter, sample, or enrich telemetry
- **Fan-out:** Send to multiple backends from one source

---

### 3. Tempo (Distributed Tracing)

**Container:** `tempo`
**Image:** `grafana/tempo:2.6.1`
**Ports:**
- `3200` - HTTP API
- `9095` - gRPC API

**Purpose:** Stores and queries distributed traces

Tempo is a **trace backend** that:
- Stores traces efficiently using object storage patterns
- Provides TraceQL for querying traces
- Generates **span metrics** (RED metrics derived from traces)
- Supports trace-to-logs and trace-to-metrics correlation

**Key Configuration:**
```yaml
metrics_generator:
  processor:
    span_metrics:
      dimensions:
        - service.name
        - http.method
        - http.route
        - http.status_code
  storage:
    remote_write:
      - url: http://prometheus:9090/api/v1/write  # Span metrics → Prometheus
```

**What you can do with Tempo:**
- View full request traces across services
- Analyze latency at each span
- Find slow or error traces
- Correlate with logs via TraceId

---

### 4. Prometheus (Metrics Storage)

**Container:** `prometheus`
**Image:** `prom/prometheus:v2.54.1`
**Port:** `9090`

**Purpose:** Time-series database for metrics

Prometheus stores all metrics data and provides PromQL for querying. It's configured with special features:

**Enabled Features:**

| Feature | Purpose |
|---------|---------|
| `exemplar-storage` | Links metrics to trace IDs (click metric → see trace) |
| `native-histograms` | Better histogram precision and efficiency |
| `remote-write-receiver` | Receives span metrics from Tempo |

**Data Sources:**
1. **OTel Collector** (scrape `:8889`) - Application metrics via OTLP
2. **Tempo** (remote write) - Span metrics generated from traces

**Key Metrics Available:**
```promql
# HTTP Server (incoming requests)
http_server_request_duration_seconds_bucket
http_server_request_duration_seconds_count

# HTTP Client (outgoing requests)
http_client_request_duration_seconds_bucket

# .NET Runtime
process_cpu_time_seconds_total
process_memory_working_set_bytes
dotnet_gc_collections_total
dotnet_thread_pool_thread_count
```

---

### 5. Loki (Log Aggregation)

**Container:** `loki`
**Image:** `grafana/loki:3.3.2`
**Port:** `3100`

**Purpose:** Log aggregation and querying

Loki is a **log aggregation system** designed to be cost-effective and easy to operate. Unlike traditional log systems, Loki only indexes labels (not full text), making it very efficient.

**Key Features:**
- Receives logs via OTLP from the collector
- Stores logs with labels for filtering
- LogQL query language for searching
- Integrates with Grafana for visualization

**How logs flow:**
```
App (ILogger) → OTel SDK → Collector → Loki
```

**Log Structure (OTLP format):**
```json
{
  "Body": "Fetching user 1 from external API",
  "SeverityText": "Information",
  "TraceId": "abc123...",
  "SpanId": "def456...",
  "Resources": {
    "service.name": "observability-demo"
  }
}
```

**Example LogQL Queries:**
```logql
# All logs
{exporter="OTLP"} | json

# Filter by level
{exporter="OTLP"} | json | SeverityText = "Error"

# Search text
{exporter="OTLP"} | json |= "user"

# Find by trace ID
{exporter="OTLP"} | json | TraceId = "abc123..."
```

---

### 6. Grafana (Visualization)

**Container:** `grafana`
**Image:** `grafana/grafana:11.4.0`
**Port:** `3000`
**Credentials:** admin / admin

**Purpose:** Unified visualization for all telemetry

Grafana provides a single pane of glass for:
- **Dashboards** - Pre-built visualizations for RED metrics, runtime stats, logs
- **Explore** - Ad-hoc querying of metrics, traces, and logs
- **Correlations** - Jump from metrics → traces → logs

**Enabled Feature Toggles:**

| Feature | Purpose |
|---------|---------|
| `traceqlEditor` | TraceQL query builder |
| `tempoSearch` | Search traces in Tempo |
| `tempoServiceGraph` | Service dependency graph |
| `traceToMetrics` | Link traces to metrics |
| `correlations` | Cross-datasource linking |

**Pre-configured Datasources:**
1. **Prometheus** - Metrics queries (PromQL)
2. **Tempo** - Trace queries (TraceQL)
3. **Loki** - Log queries (LogQL)

**Pre-built Dashboard:**
The dashboard at `/d/observability-demo-dashboard/` includes:
- RED metrics (Rate, Errors, Duration)
- Latency percentiles (p50, p95, p99)
- .NET runtime metrics (CPU, memory, GC, threads)
- Log volume and filtered log viewer

---

## Data Flow Summary

```
                                    ┌─────────────────┐
                                    │   Application   │
                                    │   (WebAPI)      │
                                    └────────┬────────┘
                                             │
                            Traces, Metrics, Logs (OTLP)
                                             │
                                             ▼
                                    ┌─────────────────┐
                                    │  OTel Collector │
                                    └────────┬────────┘
                                             │
                   ┌─────────────────────────┼─────────────────────────┐
                   │                         │                         │
                   ▼                         ▼                         ▼
           ┌──────────────┐          ┌──────────────┐          ┌──────────────┐
           │    Tempo     │          │  Prometheus  │          │    Loki      │
           │   (Traces)   │──────────│  (Metrics)   │          │   (Logs)     │
           └──────────────┘  span    └──────────────┘          └──────────────┘
                             metrics        │                         │
                                            │                         │
                                            └────────────┬────────────┘
                                                         │
                                                         ▼
                                                ┌──────────────┐
                                                │   Grafana    │
                                                │ (Dashboard)  │
                                                └──────────────┘
```

---

## Network & Volumes

### Network
All services communicate on a shared `observability` bridge network, allowing container-to-container communication using service names as hostnames.

### Volumes
| Volume | Service | Purpose |
|--------|---------|---------|
| `prometheus-data` | Prometheus | Persist metrics data |
| `tempo-data` | Tempo | Persist trace data |
| `loki-data` | Loki | Persist log data |
| `grafana-data` | Grafana | Persist dashboards, settings |

---

## Ports Reference

| Port | Service | Protocol | Purpose |
|------|---------|----------|---------|
| 8080 | WebAPI | HTTP | Application endpoints |
| 4317 | Collector | gRPC | OTLP receiver |
| 4318 | Collector | HTTP | OTLP receiver |
| 8889 | Collector | HTTP | Prometheus scrape endpoint |
| 9090 | Prometheus | HTTP | Prometheus UI & API |
| 3200 | Tempo | HTTP | Tempo API |
| 3100 | Loki | HTTP | Loki API |
| 3000 | Grafana | HTTP | Grafana UI |

---

## Quick Start

```bash
# Start everything
docker compose up -d --build

# Generate some traffic
./scripts/generate-traffic.sh

# View dashboard
open http://localhost:3000/d/observability-demo-dashboard/

# Explore logs
open http://localhost:3000/explore

# Stop everything
docker compose down

# Stop and remove data
docker compose down -v
```
