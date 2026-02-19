# Local Development

## Prerequisites
- .NET SDK `10.0.102`
- Podman `5.x`

## Build the solution
```bash
dotnet restore ObservabilityDemo.slnx
dotnet build ObservabilityDemo.slnx
```

## Start the observability stack
```bash
podman compose -f ops/compose/compose.yaml up -d --build
```

To scale API instances (horizontal validation):
```bash
podman compose -f ops/compose/compose.yaml up -d --build --scale api=3
```
Traffic enters through the `proxy` service on `localhost:8080`, and the proxy forwards to one or more `api` replicas.

Verify running containers:
```bash
podman compose -f ops/compose/compose.yaml ps
```

## Useful local endpoints
- API base: `http://localhost:8080`
- API health: `http://localhost:8080/health/live`
- Diagnostics slow: `http://localhost:8080/diagnostics/slow?delayMs=2000`
- Diagnostics fail: `http://localhost:8080/diagnostics/fail`
- Grafana: `http://localhost:3000` (`admin` / `admin`)
- Prometheus: `http://localhost:9090`

## Generate demo traffic
Use the built-in script to generate normal, slow, and failure traffic patterns:
```bash
ops/scripts/generate-traffic.sh --iterations 120 --sleep-ms 100
```

Useful options:
- `--base-url` to target a different API host.
- `--base-urls` to round-robin traffic across scaled instances.
- `--tenant-id` to pick a specific tenant.
- `--rps` to set loop rate (iterations per second).
- `--duration-seconds` to run continuously for a fixed window.
- `--slow-every` / `--fail-every` to tune latency/error frequency.
- `--slow-delay-ms` to control intentional slow-request duration.
- `--help` to list all flags and defaults.

Continuous rate example:
```bash
ops/scripts/generate-traffic.sh --rps 100 --duration-seconds 300
```

Equivalent environment-variable mode:
```bash
RPS=100 DURATION_SECONDS=300 ops/scripts/generate-traffic.sh
```

High-throughput finite run example:
```bash
ops/scripts/generate-traffic.sh --rps 100 --iterations 500 --slow-every 10 --fail-every 20
```

For one-command scale validation (scale + traffic + instance-label check):
```bash
ops/scripts/scale-validate.sh --replicas 3 --iterations 120
```
The script scales the API, runs traffic, and validates that metrics expose distinct instance labels.

## Grafana drill-down workflow
Dashboards:
- `Latency Overview (Bootstrap)`
- `Reliability And Drilldown`

Fast path (few clicks):
1. Use latency/error panels to identify a spike.
2. Click an exemplar marker (dot) on a Prometheus timeseries to open the related Tempo trace.
3. From the trace view, use trace-to-logs to pivot directly into Loki for that trace/span window.

Fallback path (if exemplar is not present at that point in time):
1. Use Prometheus panels to identify the slow route (`p95` and `Slow Requests > 500ms`).
2. In Grafana Explore (Tempo), search traces by `service.name=observability-demo-api` and duration filter.
3. Open a trace and inspect the span waterfall to find the slow segment.
4. Pivot to Loki logs using trace link (`traceid` field is auto-linked to Tempo).
5. Use `tenant_id`, `request_path`, and `http_status_code` fields to isolate affected tenant/endpoint.

Full incident workflow:
- `docs/drilldown-playbook.md`

## Tear down
```bash
podman compose -f ops/compose/compose.yaml down -v --remove-orphans
```
