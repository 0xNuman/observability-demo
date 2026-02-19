# Observability Demo - Implementation Plan

## 1. Purpose
Build a production-grade .NET 10 backend API for a multi-tenant B2B SaaS scenario, demonstrating end-to-end observability with OpenTelemetry traces, metrics, and logs under horizontal scaling.
The platform must also support percentile latency analysis (`p50`, `p95`, `p99`), SLI/SLO posture reporting, and uptime visibility for reliability conversations.

## 2. Confirmed Decisions
- Runtime: .NET 10 (target framework and SDK)
- Architecture: Clean Architecture
- Database: PostgreSQL
- Data access: Dapper
- Logging: Serilog
- Container stack: Podman + compose
- Observability stack: OpenTelemetry Collector, Tempo, Loki, Prometheus, Grafana
- Multi-tenancy model: Shared database, shared schema, partitioned by `tenant_id`
- Auth: Out of scope for first iteration

## 3. MVP Functional Scope
Implement one complete tenant-aware vertical slice:
- Core entity: `WorkItem`
- Supporting entity: `Tenant`
- Endpoints:
  - `POST /work-items`
  - `GET /work-items/{id}`
  - `GET /work-items` (filter + pagination)
  - `PATCH /work-items/{id}/status`
  - `POST /work-items/bulk-transition` (stored procedure-backed)
- Diagnostic endpoints:
  - `GET /diagnostics/slow` (intentional latency path)
  - `GET /diagnostics/fail` (intentional failure path)

## 4. Non-Functional Scope
- API instances must be horizontally scalable and distinguishable in telemetry.
- Every request must support trace to logs correlation.
- Slow and failed requests must be queryable in Grafana/Tempo/Loki/Prometheus.
- OTel-related API code must include comments explaining what and why.
- Latency percentiles (`p50`, `p95`, `p99`) must be queryable at service and endpoint level.
- Service reliability posture must be answerable using SLI/SLO dashboards and queries.
- Uptime and health status must be measurable and reportable over time windows.

## 5. Solution Structure (Clean Architecture)
Proposed .NET solution layout:

```text
src/
  ObservabilityDemo.Api/
  ObservabilityDemo.Application/
  ObservabilityDemo.Domain/
  ObservabilityDemo.Infrastructure/
tests/
  ObservabilityDemo.ArchitectureTests/
  ObservabilityDemo.IntegrationTests/
  ObservabilityDemo.Api.Tests/
ops/
  compose/
  otel/
docs/
```

Layer responsibilities:
- `Domain`: entities, value objects, domain rules, status transition policy.
- `Application`: use cases, contracts, validation, orchestration.
- `Infrastructure`: Dapper repositories, SQL scripts, telemetry bootstrapping, DB access.
- `Api`: controllers/endpoints, middleware, DI wiring, request context extraction.

## 6. Data Model and Tenant Partitioning
Tables (initial):
- `tenants`
- `work_items`
- `work_item_history`
- `outbox_messages` (optional in MVP, recommended for realistic extension)

Tenant partitioning rules:
- All tenant-scoped tables include `tenant_id`.
- Every read/write query includes `tenant_id` predicate.
- Composite indexes start with `tenant_id` where practical.
- API resolves tenant from `X-Tenant-Id` header.
- Missing/invalid tenant header returns `400`.

Example `work_items` columns:
- `id` (UUID, PK)
- `tenant_id` (UUID, indexed)
- `title`
- `description`
- `status`
- `priority`
- `created_at_utc`
- `updated_at_utc`
- `created_by`
- `updated_by`

## 7. Stored Procedure Design
Use case: bulk status transition with strict tenant safety and auditability.

Procedure:
- Name: `sp_work_items_bulk_transition`
- Inputs:
  - `p_tenant_id UUID`
  - `p_work_item_ids UUID[]`
  - `p_target_status TEXT`
  - `p_changed_by TEXT`
  - `p_correlation_id TEXT`
- Behavior:
  - Validate tenant ownership for each ID.
  - Enforce allowed status transitions.
  - Update eligible rows in one transaction.
  - Insert audit rows into `work_item_history`.
  - Return summary (`updated_count`, `rejected_count`, `details`).

Dapper integration:
- Repository method executes SP with named parameters.
- Map returned summary DTO.
- Emit structured logs with tenant, counts, and correlation fields.

## 8. Observability Design
### 8.1 OpenTelemetry signal plan
- Traces:
  - ASP.NET Core inbound requests
  - `HttpClient` outbound calls (if added)
  - Npgsql database spans
  - Custom spans around business operations where helpful
- Metrics:
  - Request duration/count/error metrics
  - Request duration histograms configured to support `p50`, `p95`, and `p99` queries
  - Availability/error-rate metrics for SLI calculations
  - DB operation duration/count
  - Custom metric for bulk transition size and outcome
- Logs:
  - Serilog structured logs
  - Correlate logs with trace/span IDs
  - Export through OTel pipeline

### 8.2 Resource attributes
Include:
- `service.name`
- `service.version`
- `service.instance.id` (unique per container instance)
- `deployment.environment`

### 8.3 Correlation and drill-down
- Enrich logs with:
  - `trace_id`
  - `span_id`
  - `tenant_id`
  - `request_path`
  - `http_status_code`
  - `instance_id`
- Ensure Grafana/Tempo/Loki labels permit pivoting:
  - from request latency to trace
  - from trace to related logs
  - from issue to specific API instance

### 8.4 OTel code comment policy
In API and infrastructure telemetry bootstrapping code:
- Add concise comments describing:
  - what instrumentation is enabled
  - why it is needed for RCA and scale diagnostics
  - why selected resource attributes are included

### 8.5 SLI/SLO and uptime model
SLIs to compute:
- Availability SLI: successful requests / total requests (exclude expected client-side validation failures if needed).
- Latency SLI: requests under threshold per endpoint category (for example read vs write).
- Error-rate SLI: proportion of server-side failures (`5xx`) over total requests.

Initial SLO targets (configurable, can be tuned after baseline measurement):
- Availability: 99.9% over rolling 30 days.
- Latency: `p95` under target thresholds per endpoint group.
- Error-rate: below 1% over rolling 30 days.

Uptime tracking:
- Track process/container uptime per API instance (`up` and health/readiness success signals).
- Dashboard uptime percentages for 24h, 7d, and 30d windows.
- Ensure uptime can be broken down by instance to identify unstable nodes.

## 9. Podman Compose Stack
Services:
- `api` (scale to multiple instances)
- `postgres`
- `otel-collector`
- `tempo`
- `loki`
- `prometheus`
- `grafana`

Configuration goals:
- OTLP from API to collector
- Collector fan-out:
  - traces -> Tempo
  - metrics -> Prometheus
  - logs -> Loki
- Grafana datasources pre-provisioned for Tempo, Loki, Prometheus

## 10. Execution Plan (Milestones)
1. Bootstrap solution and project structure
2. Implement domain and application contracts
3. Add PostgreSQL schema + migrations/scripts
4. Implement Dapper repositories and stored procedure
5. Build API endpoints and tenant middleware
6. Add OpenTelemetry + Serilog integration
7. Create Podman compose stack and config files
8. Define SLI/SLO metrics, percentile queries, and uptime dashboard panels
9. Add diagnostics endpoints for slow/fail scenarios
10. Validate scaling, telemetry, and drill-down flows
11. Add tests and finalize documentation

## 11. Validation and Acceptance Criteria
Functional:
- CRUD/read/update status flows work for valid tenant.
- Cross-tenant access is blocked by query design.
- Bulk transition SP updates only allowed rows and returns correct summary.

Observability:
- A normal request produces trace + metrics + logs with shared correlation fields.
- Slow endpoint appears in latency dashboards and trace detail.
- Failure endpoint records exception in trace and error logs.
- Scaled API instances are distinguishable by `service.instance.id`.
- `p50`, `p95`, and `p99` latency are queryable by service and endpoint.
- SLI/SLO dashboard shows current status and rolling-window compliance.

Operational:
- Podman compose up/down works reliably.
- Stack starts with healthy dependencies.
- Dashboards can be used for RCA workflow without manual data stitching.
- Uptime panels show service and per-instance uptime across 24h/7d/30d.

## 12. Risks and Mitigations
- Risk: Tenant leakage due to missing predicate.
  - Mitigation: enforce tenant-aware repository patterns and integration tests.
- Risk: Telemetry cardinality explosion from high-cardinality labels.
  - Mitigation: constrain labels, avoid unbounded values.
- Risk: SP behavior drift from application rules.
  - Mitigation: keep transition rules centralized and test SP outcomes.
- Risk: Local environment inconsistency.
  - Mitigation: keep all runtime dependencies in compose stack.

## 13. Immediate Next Step
After plan approval, scaffold the .NET 10 solution and Podman compose baseline, then implement the first end-to-end `WorkItem` slice with telemetry, percentile metrics, and SLI/SLO/uptime dashboards before expanding.
