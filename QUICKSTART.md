# Quick Start Guide

Get the observability demo running in under 2 minutes.

## Prerequisites

- Docker and Docker Compose installed
- 4GB+ available RAM (for all services)
- Ports available: 3000, 8080, 9090, 3100, 3200, 4317, 4318

## Start Everything

```bash
# Start all services (builds the .NET app automatically)
docker-compose up -d --build

# Wait for services to be healthy (~30-60 seconds)
docker-compose ps
```

## Access the Services

| Service    | URL                           | Purpose                    |
|------------|-------------------------------|----------------------------|
| Grafana    | http://localhost:3000         | Dashboards (admin/admin)   |
| WebAPI     | http://localhost:8080         | .NET application           |
| Prometheus | http://localhost:9090         | Metrics queries            |
| Tempo      | http://localhost:3200         | Trace queries              |
| Loki       | http://localhost:3100         | Log queries                |

## Generate Traffic

```bash
# Quick test (30 seconds)
DURATION=30 ./scripts/generate-traffic.sh

# Full demo (5 minutes, default)
./scripts/generate-traffic.sh

# High load test
RATE=high DURATION=60 ./scripts/generate-traffic.sh
```

## View the Dashboard

1. Open http://localhost:3000
2. Login: admin / admin
3. Go to Dashboards → Observability Demo → Observability Demo Dashboard

## Quick API Test

```bash
# Simple hello
curl http://localhost:8080/api/demo/hello

# Get user (external API call)
curl http://localhost:8080/api/demo/users/1

# Get posts
curl http://localhost:8080/api/demo/posts?limit=5

# Slow request (demonstrates latency)
curl http://localhost:8080/api/demo/slow?delayMs=500

# Error (demonstrates error tracking)
curl http://localhost:8080/api/demo/error
```

## Stop Everything

```bash
docker-compose down

# Remove all data
docker-compose down -v
```

## Troubleshooting

**Services not starting?**
```bash
# Check logs
docker-compose logs -f

# Check specific service
docker-compose logs webapi
docker-compose logs otel-collector
```

**No metrics in Grafana?**
- Wait 1-2 minutes after starting
- Generate some traffic first
- Check OTel Collector: http://localhost:55679/debug/tracez

**Build fails?**
```bash
# Rebuild from scratch
docker-compose build --no-cache webapi
```

## Next Steps

- Read [CONCEPTS.md](CONCEPTS.md) to understand observability theory
- Read [README.md](README.md) for detailed architecture and features
- Explore the Grafana dashboard panels
- Try clicking on exemplars to jump to traces!
