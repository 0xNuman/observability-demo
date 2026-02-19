#!/usr/bin/env bash

set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
TENANT_ID="${TENANT_ID:-11111111-1111-1111-1111-111111111111}"
ITERATIONS="${ITERATIONS:-250}"
SLEEP_MS="${SLEEP_MS:-150}"
SLOW_EVERY="${SLOW_EVERY:-8}"
FAIL_EVERY="${FAIL_EVERY:-15}"
SLOW_DELAY_MS="${SLOW_DELAY_MS:-1200}"

usage() {
  cat <<'EOF'
Usage:
  ops/scripts/generate-traffic.sh [options]

Options:
  --base-url URL         API base URL (default: http://localhost:8080)
  --tenant-id GUID       Tenant ID header value
  --iterations N         Number of traffic iterations (default: 50)
  --sleep-ms N           Sleep between iterations in milliseconds (default: 150)
  --slow-every N         Hit /diagnostics/slow every N iterations (default: 8)
  --fail-every N         Hit /diagnostics/fail every N iterations (default: 15)
  --slow-delay-ms N      Delay for /diagnostics/slow (default: 1200)
  -h, --help             Show this help

Environment overrides are also supported:
  BASE_URL, TENANT_ID, ITERATIONS, SLEEP_MS, SLOW_EVERY, FAIL_EVERY, SLOW_DELAY_MS
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-url)
      BASE_URL="$2"
      shift 2
      ;;
    --tenant-id)
      TENANT_ID="$2"
      shift 2
      ;;
    --iterations)
      ITERATIONS="$2"
      shift 2
      ;;
    --sleep-ms)
      SLEEP_MS="$2"
      shift 2
      ;;
    --slow-every)
      SLOW_EVERY="$2"
      shift 2
      ;;
    --fail-every)
      FAIL_EVERY="$2"
      shift 2
      ;;
    --slow-delay-ms)
      SLOW_DELAY_MS="$2"
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

if ! [[ "$ITERATIONS" =~ ^[0-9]+$ ]] || [[ "$ITERATIONS" -le 0 ]]; then
  echo "ITERATIONS must be a positive integer." >&2
  exit 1
fi

if ! [[ "$SLEEP_MS" =~ ^[0-9]+$ ]]; then
  echo "SLEEP_MS must be a non-negative integer." >&2
  exit 1
fi

status_code() {
  local code
  code="$(curl -s -o /tmp/observability-demo-traffic-response.json -w "%{http_code}" "$@")"
  printf '%s' "$code"
}

extract_id() {
  local body
  body="$(cat /tmp/observability-demo-traffic-response.json)"
  if command -v jq >/dev/null 2>&1; then
    echo "$body" | jq -r '.id // empty'
  else
    echo "$body" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p'
  fi
}

sleep_between_iterations() {
  if [[ "$SLEEP_MS" -eq 0 ]]; then
    return
  fi

  # shellcheck disable=SC2059
  printf -v __sleep_seconds '0.%03d' "$SLEEP_MS"
  sleep "$__sleep_seconds"
}

two_xx=0
four_xx=0
five_xx=0
other=0

classify_code() {
  local code="$1"
  case "${code:0:1}" in
    2) ((two_xx+=1)) ;;
    4) ((four_xx+=1)) ;;
    5) ((five_xx+=1)) ;;
    *) ((other+=1)) ;;
  esac
}

echo "Generating traffic..."
echo "BASE_URL=$BASE_URL TENANT_ID=$TENANT_ID ITERATIONS=$ITERATIONS"

for ((i=1; i<=ITERATIONS; i++)); do
  code="$(status_code "$BASE_URL/work-items?page=1&pageSize=20" -H "X-Tenant-Id: $TENANT_ID")"
  classify_code "$code"

  payload="$(cat <<EOF
{"title":"Traffic item $i","description":"Generated load item $i","priority":"High","requestedBy":"traffic-script"}
EOF
)"
  code="$(status_code -X POST "$BASE_URL/work-items" -H "Content-Type: application/json" -H "X-Tenant-Id: $TENANT_ID" -d "$payload")"
  classify_code "$code"

  work_item_id="$(extract_id)"
  if [[ -n "$work_item_id" ]]; then
    code="$(status_code -X PATCH "$BASE_URL/work-items/$work_item_id/status" -H "Content-Type: application/json" -H "X-Tenant-Id: $TENANT_ID" -d '{"status":"InProgress","updatedBy":"traffic-script"}')"
    classify_code "$code"

    bulk_payload="$(cat <<EOF
{"workItemIds":["$work_item_id"],"targetStatus":"Blocked","changedBy":"traffic-script","correlationId":"traffic-$i"}
EOF
)"
    code="$(status_code -X POST "$BASE_URL/work-items/bulk-transition" -H "Content-Type: application/json" -H "X-Tenant-Id: $TENANT_ID" -d "$bulk_payload")"
    classify_code "$code"
  fi

  if (( SLOW_EVERY > 0 && i % SLOW_EVERY == 0 )); then
    code="$(status_code "$BASE_URL/diagnostics/slow?delayMs=$SLOW_DELAY_MS")"
    classify_code "$code"
  fi

  if (( FAIL_EVERY > 0 && i % FAIL_EVERY == 0 )); then
    code="$(status_code "$BASE_URL/diagnostics/fail")"
    classify_code "$code"
  fi

  sleep_between_iterations
done

echo
echo "Traffic generation completed."
echo "2xx responses: $two_xx"
echo "4xx responses: $four_xx"
echo "5xx responses: $five_xx"
echo "other responses: $other"
echo
echo "Use Grafana to inspect traces/metrics/logs for this run:"
echo "- /work-items traffic for normal paths"
echo "- /diagnostics/slow for latency spikes"
echo "- /diagnostics/fail for error paths"
