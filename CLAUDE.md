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
dotnet test tests/ObservabilityDemo.IntegrationTests
dotnet test tests/ObservabilityDemo.ArchitectureTests
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

- **Domain** (`src/ObservabilityDemo.Domain`) — `WorkItem`, `Tenant` entities; `WorkItemStatus` (New/InProgress/Blocked/Done/Cancelled), `WorkItemPriority` (Low/Medium/High/Critical) enums. No external NuGet dependencies.
- **Application** (`src/ObservabilityDemo.Application`) — `IWorkItemService` / `WorkItemService`, command/query records (`CreateWorkItemCommand`, `UpdateWorkItemStatusCommand`, `BulkTransitionCommand`, `ListWorkItemsQuery`), DTOs (`WorkItemDto`, `WorkItemListResult`, `BulkTransitionResult`), `IWorkItemRepository` abstraction.
- **Infrastructure** (`src/ObservabilityDemo.Infrastructure`) — `DapperWorkItemRepository` (Dapper + Npgsql), `WorkItemTelemetry` custom metrics, `PostgresConnectionString` wrapper. Registered via `AddInfrastructure(IConfiguration)`.
- **API** (`src/ObservabilityDemo.Api`) — Controllers, tenant middleware, OTel/Serilog bootstrap (`ApiObservabilityExtensions`). Registered via `AddApplication()`.

### Multi-tenancy
Every request to `/work-items/*` requires an `X-Tenant-Id` header (GUID), validated by `TenantContextMiddleware`. The resolved GUID is stored in the scoped `ITenantContext` / `TenantContext` service and enriched as a log property and OpenTelemetry tag/baggage. All DB queries include a `tenant_id` predicate; composite indexes start with `tenant_id`.

### Work Item State Machine
Terminal states: **Done** and **Cancelled** — no further transitions allowed. Eligible states for transition: New, InProgress, Blocked. Business rule violations surface as `InvalidOperationException` → HTTP 409 Conflict.

### Observability Pipeline
```
API (OTLP HTTP :4318) → OTel Collector → Tempo   (traces)
                                        → Loki    (logs)
                                        → Prometheus (metrics :8889)
```
Serilog writes structured JSON logs (with trace_id, span_id, tenant_id, instance_id) to the OTel sink. Trace-based exemplar markers on Prometheus histograms link directly to Tempo traces for fast drill-down. Custom histogram buckets for HTTP request duration (5 ms – 10 s, 14 boundaries).

### Custom Metrics (`WorkItemTelemetry`)
| Instrument | Type | Tags |
|---|---|---|
| `work_items.bulk_transition.batch_size` | Histogram | `target_status` |
| `work_items.bulk_transition.updated_count` | Counter | `target_status` |
| `work_items.bulk_transition.rejected_count` | Counter | `target_status` |

### Scaling
Nginx (`ops/nginx/nginx.conf`) load-balances across API instances. Prometheus uses DNS service discovery to scrape per-instance uptime metrics from `/health/prometheus`. Instance identity comes from container hostname (set as `service.instance.id` in OTel resource).

### Database
PostgreSQL 17 (postgres:18-alpine image). Schema in `ops/postgres/init/001_schema.sql`:
- **`tenants`** — id, name, is_active, created_at_utc
- **`work_items`** — id, tenant_id (FK), title, description, status (check constraint), priority (check constraint), created/updated timestamps + actors
- **`work_item_history`** — id, tenant_id, work_item_id, action, from/to status, changed_by, correlation_id, created_at_utc

Bulk status transitions use stored procedure `sp_work_items_bulk_transition` for atomicity + audit trail in `work_item_history`.

Seed data (`ops/postgres/init/002_seed.sql`): tenant `11111111-1111-1111-1111-111111111111` (Acme Corp).

## Key Endpoints

| Method | Endpoint | Purpose | Auth |
|--------|----------|---------|------|
| GET | `/` | Service status | — |
| GET | `/health/live` | Liveness check | — |
| GET | `/health/ready` | Readiness check | — |
| GET | `/health/prometheus` | Per-instance uptime metrics (Prometheus text format) | — |
| GET | `/diagnostics/slow?delayMs=1500` | Intentional latency (0–30000 ms) | — |
| GET | `/diagnostics/fail` | Intentional 500 error | — |
| POST | `/work-items` | Create work item (201) | X-Tenant-Id |
| GET | `/work-items` | List items, paginated (?status, ?page, ?pageSize) | X-Tenant-Id |
| GET | `/work-items/{id}` | Get item by ID (200/404) | X-Tenant-Id |
| PATCH | `/work-items/{id}/status` | Update status (200/404/409) | X-Tenant-Id |
| POST | `/work-items/bulk-transition` | Bulk status transition | X-Tenant-Id |

UIs:
- Grafana: http://localhost:3000 (admin/admin)
- Prometheus: http://localhost:9090
- Tempo: http://localhost:3200

## Configuration

- `src/ObservabilityDemo.Api/appsettings.json` — OTLP endpoint (`otel-collector:4318` in container, `localhost:4318` locally), PostgreSQL connection string, Serilog levels, service name
- `ops/otel/otel-collector-config.yaml` — OTel Collector pipeline (receivers gRPC :4317 / HTTP :4318, batch processor, exporters to Tempo/Loki/Prometheus :8889)
- `ops/prometheus/prometheus.yml` — Scrape configs: `otel-collector` (static), `api-uptime` (DNS SD → `/health/prometheus`), self-monitoring
- `ops/tempo/tempo.yaml` — HTTP :3200, OTLP gRPC :4317/HTTP :4318, local storage /tmp/tempo, 24h retention
- `ops/loki/loki-config.yaml` — HTTP :3100, TSDB filesystem storage, auth disabled
- `ops/nginx/nginx.conf` — Proxy pass to `api:8080`, forwards X-Forwarded-For/Proto
- `ops/grafana/provisioning/` — Auto-provisioned datasources and dashboards

## Container Services

8 services defined in `ops/compose/compose.yaml`:

| Service | Image | Port(s) |
|---------|-------|---------|
| api | local Dockerfile | 8080 (via proxy) |
| proxy | nginx:1.29-alpine | 8080 |
| postgres | postgres:18-alpine | 5432 |
| otel-collector | otel/opentelemetry-collector-contrib:0.126.0 | 4317, 4318, 8889 |
| tempo | grafana/tempo:2.10.1 | 3200, 4319 |
| loki | grafana/loki:3.5.4 | 3100 |
| prometheus | prom/prometheus:v3.6.0 | 9090 |
| grafana | grafana/grafana:12.2.0 | 3000 |

## Test Projects

| Project | Type | Key Coverage |
|---------|------|-------------|
| `tests/ObservabilityDemo.Api.Tests` | Endpoint (WebApplicationFactory) | Tenant header validation, CRUD happy/sad paths, Prometheus health endpoint |
| `tests/ObservabilityDemo.IntegrationTests` | Service-layer integration | Pagination validation, bulk deduplication, terminal-state transition rejection |
| `tests/ObservabilityDemo.ArchitectureTests` | Architecture validation | Placeholder for future arch rules |

Test helpers: `ApiTestFactory` (replaces `IWorkItemService` with `StubWorkItemService`), `FakeWorkItemRepository` (in-memory, tenant-isolated).

## SDK & Tooling

- .NET SDK pinned to 10.0.102 (`global.json`, `rollForward: latestFeature`)
- `Directory.Build.props`: `LangVersion: preview`, `Nullable: enable`, `ImplicitUsings: enable`, `TreatWarningsAsErrors: false`
- Solution file: `ObservabilityDemo.slnx` (modern SLNX format)
- Container runtime: Podman (not Docker)

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.14.0 | OTLP export |
| OpenTelemetry.Extensions.Hosting | 1.14.0 | OTel host integration |
| OpenTelemetry.Instrumentation.AspNetCore | 1.14.0 | HTTP span instrumentation |
| OpenTelemetry.Instrumentation.Http | 1.14.0 | HTTP client traces |
| OpenTelemetry.Instrumentation.Runtime | 1.14.0 | Runtime metrics |
| Npgsql.OpenTelemetry | (via Npgsql) | Database span instrumentation |
| Serilog.AspNetCore | 9.0.0 | Structured request logging |
| Serilog.Formatting.Compact | 3.0.0 | Compact JSON log formatting |
| Serilog.Sinks.OpenTelemetry | 4.2.0 | Logs → OTel pipeline |
| Dapper | 2.1.66 | Lightweight SQL ORM |
| Npgsql | 9.0.3 | Async PostgreSQL driver |
| Microsoft.AspNetCore.OpenApi | 10.0.2 | OpenAPI / Swagger |
| xunit | 2.9.3 | Unit/integration test framework |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.2 | In-process test client |

## Conventions

- **Sealed classes** everywhere (anti-inheritance by default)
- **Records** for immutable DTOs, commands, and queries
- **Async-all-the-way** with `CancellationToken` on all async methods
- **Nullable reference types** enabled (`#nullable enable` globally)
- **Error mapping**: `ArgumentException` → 400, `InvalidOperationException` → 409, unhandled → 500
- **DI registration**: extension methods `AddApplication()` / `AddInfrastructure()` on `IServiceCollection`
- **Max page size**: 200; **default actor**: `"api"`; **max actor length**: 100 chars

## Scripts

- `ops/scripts/generate-traffic.sh` — Generates realistic mixed load (list, create, update, bulk-transition, slow/fail injection). Key flags: `--iterations`, `--rps`, `--duration-seconds`, `--sleep-ms`, `--slow-every`, `--fail-every`, `--tenant-id`, `--base-url`.
- `ops/scripts/scale-validate.sh` — Scales to N API replicas, generates traffic, and verifies Prometheus sees distinct instance labels. Key flags: `--replicas`, `--iterations`.

## Reference Docs

- `docs/drilldown-playbook.md` — Step-by-step incident investigation via exemplars
- `docs/observability-queries.md` — Ready-to-use PromQL/LogQL for latency percentiles, SLI/SLO, uptime
- `docs/local-development.md` — Full operational guide including scaling and verification steps
- `docs/implementation-plan.md` — Feature implementation guide
- `docs/progress-state.md` — Milestone status
- `docs/instructions.md` — General project instructions
