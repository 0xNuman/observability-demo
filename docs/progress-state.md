# Progress State (Checkpoint)

Updated: 2026-02-19

## Resume Protocol
To resume without scanning the full repo, read only:
1. `instructions.md`
2. `docs/implementation-plan.md`
3. `docs/progress-state.md`

## Current Milestone Status
- Milestone 1 (scaffold baseline): complete
- Milestone 2 (first tenant-aware vertical slice): complete
- Milestone 3 (PostgreSQL schema/scripts): complete
- Milestone 4 (Dapper + stored procedure wiring): complete
- Milestone 5 (API endpoints + tenant middleware): complete
- Milestone 6 (OpenTelemetry + Serilog code wiring): complete
- Milestone 7 (Podman compose stack + config files): complete
- Milestone 10 (scaling/telemetry drill-down validation): in progress (single-instance smoke complete)

## Completed Work
- Created solution in new format: `ObservabilityDemo.slnx` (legacy `.sln` removed).
- Scaffolded Clean Architecture projects under `src/`:
  - `ObservabilityDemo.Api`
  - `ObservabilityDemo.Application`
  - `ObservabilityDemo.Domain`
  - `ObservabilityDemo.Infrastructure`
- Added test projects under `tests/`:
  - `ObservabilityDemo.Api.Tests`
  - `ObservabilityDemo.IntegrationTests`
  - `ObservabilityDemo.ArchitectureTests`
- Wired project references across layers and tests.
- Replaced template weather endpoint with:
  - `DiagnosticsController` (`/diagnostics/slow`, `/diagnostics/fail`)
  - `WorkItemsController` placeholder endpoints
- Added baseline domain model:
  - `Tenant`, `WorkItem`
  - `WorkItemPriority`, `WorkItemStatus`
- Added baseline DI extension points:
  - `AddApplication()`
  - `AddInfrastructure()`
- Added ops baseline:
  - Podman compose stack (`ops/compose/compose.yaml`)
  - OTel Collector config
  - Tempo/Loki/Prometheus/Grafana configs
  - Grafana datasource + starter dashboard JSON
  - Postgres schema + seed SQL
- Added docs:
  - `docs/local-development.md`
  - `docs/implementation-plan.md`
- Implemented tenant context and validation middleware in API:
  - `X-Tenant-Id` required and validated for `/work-items` endpoints
  - Invalid/missing tenant header returns `400` with problem details
- Implemented Application WorkItem contracts + service orchestration:
  - create/get/list/update-status/bulk-transition command/query models
  - `IWorkItemService` + `WorkItemService`
  - `IWorkItemRepository` abstraction
- Implemented Infrastructure Dapper repository with tenant-safe predicates:
  - tenant-filtered create/get/list/count/update status queries
  - stored procedure integration for `sp_work_items_bulk_transition`
  - PostgreSQL connection string wiring through DI
- Replaced `WorkItemsController` placeholders with working endpoints backed by Application services.
- Added required package references for Milestone 2 data-access implementation:
  - `Dapper`
  - `Npgsql`
  - `Microsoft.Extensions.Configuration.Abstractions`
- Implemented OpenTelemetry + Serilog wiring in API startup:
  - OTLP export for traces, metrics, and logs
  - resource attributes (`service.name`, `service.version`, `service.instance.id`, `deployment.environment`)
  - ASP.NET Core + HttpClient auto instrumentation
  - Npgsql auto span collection (`AddSource("Npgsql")`)
  - explicit HTTP duration histogram buckets for percentile queries
- Added request log enrichment and correlation fields:
  - `trace_id`, `span_id`, `tenant_id`, `request_path`, `http_status_code`, `instance_id`
- Added tenant tag/baggage enrichment for trace-level tenant correlation.
- Added custom business metrics for bulk transition:
  - batch size histogram
  - updated/rejected counters
- Added verification-focused tests for Milestone 2 behavior:
  - API endpoint tests with `WebApplicationFactory` for tenant header validation and WorkItem flows
  - Application service tests for pagination/transition validation behavior
- Added traffic generation script for observability demos:
  - `ops/scripts/generate-traffic.sh`
  - generates normal + slow + failing traffic patterns for Grafana/Tempo/Loki validation

## Important Fix Applied
Build error from missing `IServiceCollection` references was addressed by adding:
- `Microsoft.Extensions.DependencyInjection.Abstractions` to:
  - `src/ObservabilityDemo.Application/ObservabilityDemo.Application.csproj`
  - `src/ObservabilityDemo.Infrastructure/ObservabilityDemo.Infrastructure.csproj`
- Follow-up compile issue in Infrastructure DI (`PostgresConnectionString` type resolution) was fixed by adding the missing namespace import.
- Telemetry compile issue in Infrastructure (`TagList` resolution) was fixed by adding the required `System.Diagnostics` namespace import.
- API test JSON enum deserialization mismatch was fixed by using explicit `JsonStringEnumConverter` in test client parsing.
- OTel collector receiver binding issue was fixed by explicitly binding OTLP gRPC/HTTP receivers to `0.0.0.0` in `ops/otel/otel-collector-config.yaml`.
- OTLP export path issue was fixed by using explicit signal paths:
  - traces: `/v1/traces`
  - metrics: `/v1/metrics`
  - logs: `/v1/logs`
  This resolved `404` export responses and enabled end-to-end telemetry ingestion.

## Verification Completed
- `dotnet restore` completed successfully (2026-02-19, ~0.5s).
- `dotnet build` completed successfully for all projects (2026-02-19, ~2.7s).
- Build baseline is green and ready for Milestone 2 implementation.
- Milestone 2 changes also verified:
  - `dotnet restore ObservabilityDemo.slnx -v minimal` (success)
  - `dotnet build ObservabilityDemo.slnx --no-restore -v minimal` (success)
  - `dotnet test ObservabilityDemo.slnx --no-build -v minimal` (success)
- Observability and test expansion changes re-verified:
  - `dotnet restore ObservabilityDemo.slnx -v minimal` (success, no unresolved package warnings)
  - `dotnet build ObservabilityDemo.slnx --no-restore -v minimal` (success, 0 warnings/0 errors)
  - `dotnet test ObservabilityDemo.slnx --no-build -v minimal` (success, all test projects passing)
- Compose smoke verification completed:
  - `podman compose -f ops/compose/compose.yaml up -d --build` (stack up and healthy)
  - API smoke results:
    - `/health/live` `200`
    - `/health/ready` `200`
    - `/diagnostics/slow` `200`
    - `/diagnostics/fail` `500` (intentional failure path)
    - `/work-items` tenant header validation `400` for missing/invalid header
    - WorkItem create/get/list/patch/bulk-transition flows `2xx` with tenant isolation
  - Telemetry smoke results:
    - traces ingested in Tempo (search and trace-id lookup successful)
    - logs ingested in Loki (`service_name=observability-demo-api` present)
    - metrics exported via collector Prometheus endpoint, including:
      - `http_server_request_duration_seconds_*`
      - `work_items_bulk_transition_*`

## Next Steps (Immediate)
1. Add/adjust SLI/SLO + uptime panels/queries to productionize dashboards.
2. Validate multi-instance behavior by scaling API containers and confirming per-instance breakdowns.
3. Add architecture/integration tests for telemetry-specific expectations where practical.
