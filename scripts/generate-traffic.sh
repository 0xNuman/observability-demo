#!/usr/bin/env bash

# =============================================================================
# Traffic Generator for Observability Demo
# Generates varied traffic patterns to demonstrate different observability scenarios
# =============================================================================

# Configuration
BASE_URL="${BASE_URL:-http://localhost:8080}"
DURATION="${DURATION:-300}"  # Default 5 minutes
RATE="${RATE:-medium}"       # low, medium, high

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Print banner
echo -e "${BLUE}"
echo "╔══════════════════════════════════════════════════════════════╗"
echo "║           Observability Demo Traffic Generator               ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo -e "${NC}"

# Set rate parameters based on RATE setting
case $RATE in
    low)
        HELLO_INTERVAL=2
        USER_INTERVAL=5
        POST_INTERVAL=8
        SLOW_INTERVAL=10
        ERROR_INTERVAL=30
        ;;
    medium)
        HELLO_INTERVAL=0.5
        USER_INTERVAL=2
        POST_INTERVAL=3
        SLOW_INTERVAL=4
        ERROR_INTERVAL=15
        ;;
    high)
        HELLO_INTERVAL=0.1
        USER_INTERVAL=0.5
        POST_INTERVAL=1
        SLOW_INTERVAL=2
        ERROR_INTERVAL=5
        ;;
    *)
        echo -e "${RED}Invalid RATE: $RATE. Use: low, medium, high${NC}"
        exit 1
        ;;
esac

echo -e "${GREEN}Configuration:${NC}"
echo "  Base URL:  $BASE_URL"
echo "  Duration:  ${DURATION}s"
echo "  Rate:      $RATE"
echo ""

# Check if API is available
echo -e "${YELLOW}Checking API availability...${NC}"
if ! curl -s -o /dev/null -w "%{http_code}" "$BASE_URL/health" | grep -q "200"; then
    echo -e "${RED}Error: API is not available at $BASE_URL${NC}"
    echo "Make sure to run: docker-compose up -d"
    exit 1
fi
echo -e "${GREEN}API is available!${NC}"
echo ""

# Simple counters (no associative arrays needed)
HELLO_COUNT=0
USERS_COUNT=0
POSTS_COUNT=0
SLOW_COUNT=0
ERROR_COUNT=0
SUCCESS_COUNT=0
FAILURE_COUNT=0

# Cleanup function
cleanup() {
    echo ""
    echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"
    echo -e "${GREEN}Traffic Generation Complete!${NC}"
    echo ""
    echo "Statistics:"
    echo "  Hello requests:  $HELLO_COUNT"
    echo "  User requests:   $USERS_COUNT"
    echo "  Post requests:   $POSTS_COUNT"
    echo "  Slow requests:   $SLOW_COUNT"
    echo "  Error requests:  $ERROR_COUNT"
    echo ""
    echo "  Total successful: $SUCCESS_COUNT"
    echo "  Total failed:     $FAILURE_COUNT"
    echo -e "${BLUE}═══════════════════════════════════════════════════════════════${NC}"

    # Kill all background processes
    jobs -p | xargs -r kill 2>/dev/null || true
    exit 0
}

trap cleanup SIGINT SIGTERM EXIT

# Function to make a request
make_request() {
    local endpoint=$1
    local name=$2

    response=$(curl -s -o /dev/null -w "%{http_code}" "$BASE_URL$endpoint" 2>/dev/null || echo "000")

    if [[ "$response" =~ ^2[0-9][0-9]$ ]]; then
        SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
        echo -e "  ${GREEN}✓${NC} $endpoint → $response"
    else
        FAILURE_COUNT=$((FAILURE_COUNT + 1))
        echo -e "  ${RED}✗${NC} $endpoint → $response"
    fi
}

# Traffic generation functions
generate_hello_traffic() {
    while true; do
        make_request "/api/demo/hello" "hello"
        HELLO_COUNT=$((HELLO_COUNT + 1))
        sleep $HELLO_INTERVAL
    done
}

generate_user_traffic() {
    while true; do
        # Random user ID between 1 and 10
        user_id=$((RANDOM % 10 + 1))
        make_request "/api/demo/users/$user_id" "users"
        USERS_COUNT=$((USERS_COUNT + 1))
        sleep $USER_INTERVAL
    done
}

generate_post_traffic() {
    while true; do
        # Random limit between 5 and 20
        limit=$((RANDOM % 16 + 5))
        make_request "/api/demo/posts?limit=$limit" "posts"
        POSTS_COUNT=$((POSTS_COUNT + 1))
        sleep $POST_INTERVAL
    done
}

generate_slow_traffic() {
    while true; do
        # Variable delay to create interesting latency distributions
        # Mostly fast (50ms), sometimes medium (200ms), rarely slow (500-2000ms)
        rand=$((RANDOM % 100))
        if [ $rand -lt 70 ]; then
            delay=$((RANDOM % 50 + 20))      # 20-70ms (70% of requests)
        elif [ $rand -lt 90 ]; then
            delay=$((RANDOM % 200 + 100))    # 100-300ms (20% of requests)
        else
            delay=$((RANDOM % 1500 + 500))   # 500-2000ms (10% of requests)
        fi
        make_request "/api/demo/slow?delayMs=$delay" "slow"
        SLOW_COUNT=$((SLOW_COUNT + 1))
        sleep $SLOW_INTERVAL
    done
}

generate_error_traffic() {
    while true; do
        make_request "/api/demo/error" "error"
        ERROR_COUNT=$((ERROR_COUNT + 1))
        sleep $ERROR_INTERVAL
    done
}

# Start traffic generation
echo -e "${YELLOW}Starting traffic generation for ${DURATION}s...${NC}"
echo -e "${YELLOW}Press Ctrl+C to stop early${NC}"
echo ""

# Start all traffic generators in background
generate_hello_traffic &
generate_user_traffic &
generate_post_traffic &
generate_slow_traffic &
generate_error_traffic &

# Run for specified duration
sleep $DURATION

# Cleanup will be called by trap
