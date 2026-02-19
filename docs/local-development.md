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

## Tear down
```bash
podman compose -f ops/compose/compose.yaml down -v
```
