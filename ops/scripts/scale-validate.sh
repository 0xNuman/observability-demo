#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/ops/compose/compose.yaml"
REPLICAS="${REPLICAS:-3}"
ITERATIONS="${ITERATIONS:-90}"

usage() {
  cat <<'EOF'
Usage:
  ops/scripts/scale-validate.sh [options]

Options:
  --replicas N       Number of API replicas (default: 3)
  --iterations N     Traffic iterations for validation load (default: 90)
  -h, --help         Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --replicas)
      REPLICAS="$2"
      shift 2
      ;;
    --iterations)
      ITERATIONS="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if ! [[ "$REPLICAS" =~ ^[0-9]+$ ]] || [[ "$REPLICAS" -lt 2 ]]; then
  echo "REPLICAS must be an integer >= 2." >&2
  exit 1
fi

if ! [[ "$ITERATIONS" =~ ^[0-9]+$ ]] || [[ "$ITERATIONS" -le 0 ]]; then
  echo "ITERATIONS must be a positive integer." >&2
  exit 1
fi

echo "Starting compose stack with API replicas=$REPLICAS..."
podman compose -f "$COMPOSE_FILE" down --remove-orphans >/dev/null 2>&1 || true
podman compose -f "$COMPOSE_FILE" up -d --build --scale api="$REPLICAS"

echo "Waiting for API containers to initialize..."
sleep 8

api_containers=()
while IFS= read -r container_name; do
  if [[ -n "$container_name" ]]; then
    api_containers+=("$container_name")
  fi
done < <(
  podman ps \
    --filter 'label=io.podman.compose.project=observability-demo' \
    --filter 'label=io.podman.compose.service=api' \
    --format '{{.Names}}'
)
if [[ "${#api_containers[@]}" -lt "$REPLICAS" ]]; then
  echo "Expected at least $REPLICAS API containers but found ${#api_containers[@]}." >&2
  podman ps --format '{{.Names}}\t{{.Status}}'
  exit 1
fi

echo "Detected API containers: ${api_containers[*]}"

"$ROOT_DIR/ops/scripts/generate-traffic.sh" \
  --base-url "http://localhost:8080" \
  --iterations "$ITERATIONS" \
  --sleep-ms 60 \
  --slow-every 4 \
  --fail-every 7 \
  --slow-delay-ms 350

instance_count="0"
for _ in {1..12}; do
  sleep 5
  metrics_payload="$(curl -s http://localhost:8889/metrics)"
  request_count_lines="$(
    printf '%s\n' "$metrics_payload" \
      | grep 'http_server_request_duration_seconds_count' || true
  )"
  instance_count="$(
    printf '%s\n' "$request_count_lines" \
      | sed -n 's/.*service_instance_id="\([^"]*\)".*/\1/p' \
      | sort -u \
      | wc -l \
      | tr -d ' '
  )"

  if [[ "$instance_count" -lt 2 ]]; then
    instance_count="$(
      printf '%s\n' "$request_count_lines" \
        | sed -n 's/.*instance="\([^"]*\)".*/\1/p' \
        | sort -u \
        | wc -l \
        | tr -d ' '
    )"
  fi

  if [[ -z "$instance_count" ]]; then
    instance_count="0"
  fi

  if [[ "$instance_count" -ge 2 ]]; then
    break
  fi
done

echo "Distinct metric instance labels: $instance_count"
if [[ "$instance_count" -lt 2 ]]; then
  echo "Expected at least 2 distinct API instance labels in metrics output." >&2
  exit 1
fi

echo "Scale validation succeeded."
echo "Next: open Grafana and filter by instance label to compare nodes."
