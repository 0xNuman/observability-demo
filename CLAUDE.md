# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Production-grade .NET 10 observability demo: a multi-tenant B2B SaaS backend API showcasing end-to-end OpenTelemetry (traces, metrics, logs) with Grafana, Prometheus, Tempo, and Loki. The primary goal is demonstrating drill-down investigation workflows from metrics → traces → logs.

## Commands

### Build & Test
```bash
dotnet restore ObservabilityDemo.slnx
dotnet build ObservabilityDemo.slnx
dotnet test ObservabilityDemo.slnx
dotnet test ObservabilityDemo.slnx --no-build -v minimal

# Run a single test project
dotnet test tests/ObservabilityDemo.Api.Tests
```

### Container Stack (Podman)
```bash
# Start all 8 services (API, proxy, postgres, otel-collector, tempo, loki, prometheus, grafana)
podman compose -f ops/compose/compose.yaml up -d --build

# Scale API horizontally
podman compose -f ops/compose/compose.yaml up -d --build --scale api=3

podman compose -f ops/compose/compose.yaml ps
podman compose -f ops/compose/compose.yaml down -v --remove-orphans
```

### Traffic Generation
```bash
ops/scripts/generate-traffic.sh --iterations 120 --sleep-ms 100
ops/scripts/generate-traffic.sh --rps 100 --duration-seconds 300
ops/scripts/scale-validate.sh --replicas 3 --iterations 120
```

## Architecture

Clean Architecture with 4 layers:

- **Domain** (`src/ObservabilityDemo.Domain`) — `WorkItem`, `Tenant` entities; `WorkItemStatus`, `WorkItemPriority` enums
- **Application** (`src/ObservabilityDemo.Application`) — `WorkItemService`, command/query models, `IWorkItemRepository` abstraction
- **Infrastructure** (`src/ObservabilityDemo.Infrastructure`) — `DapperWorkItemRepository` (Dapper + Npgsql), `WorkItemTelemetry` custom metrics
- **API** (`src/ObservabilityDemo.Api`) — Controllers, tenant middleware, OTel/Serilog bootstrap (`ApiObservabilityExtensions`)

### Multi-tenancy
Every request requires `X-Tenant-Id` header (validated by `TenantContextMiddleware`). All DB queries include a `tenant_id` predicate; composite indexes start with `tenant_id`.

### Observability Pipeline
```
API (OTLP HTTP :4318) → OTel Collector → Tempo (traces)
                                        → Loki (logs)
                                        → Prometheus (metrics :8889)
```
Serilog writes structured logs to OTel. Exemplar markers on Prometheus metrics link directly to traces in Tempo for fast drill-down.

### Scaling
Nginx (`ops/nginx/nginx.conf`) load-balances across API instances. Prometheus uses DNS service discovery to scrape per-instance uptime metrics from `/health/prometheus`. Instance identity comes from container hostname.

### Database
PostgreSQL 17. Schema in `ops/postgres/init/001_schema.sql`. Bulk status transitions use the stored procedure `sp_work_items_bulk_transition` for atomicity and audit trail (`work_item_history` table).

## Key Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /health/live`, `/health/ready` | Health checks |
| `GET /health/prometheus` | Uptime metrics (scraped by Prometheus) |
| `GET /diagnostics/slow?delayMs=2000` | Intentional latency for testing |
| `GET /diagnostics/fail` | Intentional failures for testing |
| Grafana: http://localhost:3000 (admin/admin) | Dashboards |
| Prometheus: http://localhost:9090 | Metrics explorer |
| Tempo: http://localhost:3200 | Trace explorer |

## Configuration

- `src/ObservabilityDemo.Api/appsettings.json` — OTLP endpoint (`otel-collector:4318` in container, `localhost:4318` locally), PostgreSQL connection string, Serilog levels
- `ops/otel/otel-collector-config.yaml` — OTel Collector pipeline (receivers gRPC :4317 / HTTP :4318, exporters to Tempo/Loki/Prometheus)
- `ops/prometheus/prometheus.yml` — Scrape configs including DNS-based per-instance uptime
- `ops/grafana/provisioning/` — Auto-provisioned datasources and dashboards

## SDK & Tooling

- .NET SDK pinned to 10.0.102 (`global.json`, `rollForward: latestFeature`)
- `Directory.Build.props`: `LangVersion: preview`, `Nullable: enable`, `ImplicitUsings: enable`
- Solution file: `ObservabilityDemo.slnx` (modern SLNX format)
- Container runtime: Podman (not Docker)

## Reference Docs

- `docs/drilldown-playbook.md` — Step-by-step incident investigation via exemplars
- `docs/observability-queries.md` — Ready-to-use PromQL/LogQL for latency percentiles, SLI/SLO, uptime
- `docs/local-development.md` — Full operational guide including scaling and verification steps
