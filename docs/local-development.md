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

To scale API instances:
```bash
podman compose -f ops/compose/compose.yaml up -d --scale api=3
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
- `--tenant-id` to pick a specific tenant.
- `--slow-every` / `--fail-every` to tune latency/error frequency.
- `--slow-delay-ms` to control intentional slow-request duration.

## Tear down
```bash
podman compose -f ops/compose/compose.yaml down -v
```
