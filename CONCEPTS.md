# Observability Concepts

This document explains the observability concepts demonstrated in this project, designed for experienced developers new to comprehensive observability.

## Table of Contents

- [The Three Pillars of Observability](#the-three-pillars-of-observability)
- [The RED Method](#the-red-method)
- [Understanding Latency Percentiles](#understanding-latency-percentiles)
- [TTFB: Time To First Byte](#ttfb-time-to-first-byte)
- [Exemplars: The Magic Link](#exemplars-the-magic-link)
- [Distributed Tracing](#distributed-tracing)
- [Service Level Objectives (SLOs)](#service-level-objectives-slos)
- [When to Use What](#when-to-use-what)

---

## The Three Pillars of Observability

Modern observability is built on three complementary signal types:

### 1. Metrics

**What**: Numeric measurements collected over time (counters, gauges, histograms)

**When to use**:
- Alerting on thresholds
- Dashboards showing system health
- Capacity planning
- Identifying *that* something is wrong

**Example**:
```
http_requests_total{status="200", endpoint="/api/users"} 15423
```

**In this demo**: Request rates, error rates, latency percentiles, CPU/memory usage

### 2. Traces

**What**: The journey of a request through your system

**When to use**:
- Understanding *why* something is slow
- Debugging specific requests
- Mapping service dependencies
- Root cause analysis

**Example**:
```
Trace ID: abc123
├── WebAPI: GET /api/users/1 (150ms)
│   └── HttpClient: GET jsonplaceholder.com/users/1 (120ms)
```

**In this demo**: Automatic ASP.NET Core and HttpClient traces, custom business spans

### 3. Logs

**What**: Timestamped text records of events

**When to use**:
- Detailed debugging information
- Audit trails
- Capturing context that doesn't fit in traces
- Error messages and stack traces

**Example**:
```json
{"timestamp": "2024-01-15T10:30:00Z", "level": "Error", "message": "User not found", "userId": 999, "traceId": "abc123"}
```

**In this demo**: Structured JSON logs with trace ID correlation

---

## The RED Method

RED is a methodology for instrumenting and monitoring services:

| Letter | Metric | Question it Answers |
|--------|--------|---------------------|
| **R**ate | Requests per second | How busy is my service? |
| **E**rrors | Failed requests per second | Is my service healthy? |
| **D**uration | Latency distribution | How fast is my service? |

### Why RED Works

- **Simple**: Only three metrics to track
- **Universal**: Works for any request-based service
- **Actionable**: Directly relates to user experience

### In This Demo

```promql
# Rate: Requests per second
sum(rate(demo_requests_total[1m])) by (endpoint)

# Errors: Error percentage
sum(rate(demo_requests_total{status_class="5xx"}[1m]))
/
sum(rate(demo_requests_total[1m])) * 100

# Duration: Latency percentiles
histogram_quantile(0.95, rate(demo_request_duration_bucket[5m]))
```

---

## Understanding Latency Percentiles

Percentiles tell you what percentage of requests completed within a certain time.

### The Key Percentiles

| Percentile | Meaning | Use Case |
|------------|---------|----------|
| **p50** (median) | 50% of requests faster than this | Typical user experience |
| **p95** | 95% of requests faster than this | Most users' worst experience |
| **p99** | 99% of requests faster than this | Your slowest "normal" requests |
| **p99.9** | 99.9% faster | Edge cases, often outliers |

### Why Not Use Average?

Averages hide problems! Consider these two scenarios:

**Scenario A**: 100 requests, all take 100ms
- Average: 100ms ✓
- p99: 100ms ✓

**Scenario B**: 99 requests take 50ms, 1 takes 5050ms
- Average: 100ms ✓ (looks the same!)
- p99: 5050ms ✗ (reveals the problem!)

### Which Percentile Should I Use?

| Goal | Recommended Percentile |
|------|------------------------|
| General monitoring | p50 + p95 |
| SLO definition | p95 or p99 |
| Detecting outliers | p99 or p99.9 |
| Capacity planning | p50 |

### In This Demo

The dashboard shows p50, p95, and p99 for request duration. The `/slow` endpoint with variable delays demonstrates how these percentiles differ based on the latency distribution.

---

## TTFB: Time To First Byte

### What is TTFB?

**Time To First Byte (TTFB)** measures the time from when a client sends a request to when it receives the first byte of the response.

```
Client Request → [Network] → Server Processing → [Network] → First Byte
                            ↑                              ↑
                            └──────── TTFB ────────────────┘
```

### TTFB Components

1. **DNS Lookup**: Resolving domain to IP
2. **TCP Connection**: Establishing connection
3. **TLS Handshake**: Encrypting the connection (HTTPS)
4. **Request Transfer**: Sending request to server
5. **Server Processing**: Your application logic ← *This is what we measure*
6. **Response Transfer**: First byte back to client

### Why TTFB Matters

- **User Perception**: TTFB directly impacts perceived performance
- **Server Health**: High TTFB indicates server-side issues
- **Bottleneck Identification**: Helps distinguish network vs server problems

### TTFB vs Total Duration

| Metric | Measures | Includes Response Body |
|--------|----------|------------------------|
| TTFB | Time to first byte | No |
| Duration | Total request time | Yes |

For APIs returning small JSON payloads, these are nearly identical. For large downloads, they differ significantly.

### In This Demo

ASP.NET Core's automatic instrumentation captures `http.server.request.duration`, which represents the server-side processing time (essentially TTFB without network latency).

---

## Exemplars: The Magic Link

### What Are Exemplars?

Exemplars are sample data points attached to metrics that include a link to the trace that generated them.

```
Metric: http_request_duration_seconds
Value: 1.5
Exemplar: {traceId: "abc123", spanId: "def456"}
         ↓
         Click to view the full trace!
```

### Why Exemplars Are Powerful

Without exemplars:
1. See spike in p99 latency
2. Manually search for slow traces around that time
3. Hope you find the right one

With exemplars:
1. See spike in p99 latency
2. Click the exemplar dot
3. Immediately view the exact trace that caused the spike

### How Exemplars Flow in This Demo

```
.NET App → OTel Collector → Prometheus (stores exemplars)
                         ↘ Tempo (stores traces)
                               ↓
                         Grafana (links them!)
```

### Seeing Exemplars in Action

1. Generate traffic with variable latency:
   ```bash
   curl "http://localhost:8080/api/demo/slow?delayMs=2000"
   ```
2. Open Grafana dashboard
3. Look at "Request Duration Percentiles" panel
4. Hover over the data points - some have small squares (exemplars)
5. Click an exemplar → Jumps directly to the trace!

---

## Distributed Tracing

### What is a Trace?

A trace represents the entire journey of a request through your system.

### Trace Structure

```
Trace (unique ID: abc123)
├── Span: HTTP GET /api/users/1 (parent)
│   ├── Start: 10:00:00.000
│   ├── Duration: 150ms
│   ├── Tags: http.method=GET, http.status_code=200
│   │
│   └── Span: HttpClient GET jsonplaceholder.com (child)
│       ├── Start: 10:00:00.010
│       ├── Duration: 120ms
│       └── Tags: http.url=..., server.address=...
```

### Key Concepts

| Term | Definition |
|------|------------|
| **Trace** | Collection of spans sharing a trace ID |
| **Span** | Single operation within a trace |
| **Parent Span** | The span that initiated this operation |
| **Child Span** | A span initiated by another span |
| **Tags/Attributes** | Key-value metadata on a span |
| **Events** | Timestamped annotations within a span |

### Context Propagation

When Service A calls Service B, trace context (trace ID, span ID) must be passed along:

```
Service A                          Service B
[Span A] ────HTTP Request────────→ [Span B]
         traceparent: 00-abc123-def456-01
```

In this demo, HttpClient automatically propagates context to jsonplaceholder.com (though it doesn't participate in tracing, we still see the outgoing call).

### In This Demo

```bash
# Call that creates distributed trace
curl http://localhost:8080/api/demo/users/1
```

View in Tempo:
1. Go to Grafana → Explore → Tempo
2. Search for traces
3. Click a trace to see the span hierarchy

---

## Service Level Objectives (SLOs)

### What is an SLO?

An SLO is a target level of reliability for your service.

### SLO Components

| Component | Definition | Example |
|-----------|------------|---------|
| **SLI** (Indicator) | What you measure | Request latency |
| **SLO** (Objective) | Your target | p99 < 200ms |
| **Error Budget** | Allowed failures | 0.1% of requests |

### Common SLO Patterns

**Availability SLO**:
```
99.9% of requests return a successful response
```

**Latency SLO**:
```
95% of requests complete in < 200ms
99% of requests complete in < 500ms
```

### Calculating Error Budget

If your SLO is 99.9% availability:
- Error budget = 0.1% of requests can fail
- Over 30 days: 0.1% × 30 days = ~43 minutes of downtime allowed

### SLOs in Practice

This demo provides the metrics needed for SLO monitoring:

```promql
# Availability SLI
sum(rate(http_requests_total{status!~"5.."}[5m]))
/
sum(rate(http_requests_total[5m]))

# Latency SLI (% of requests under 200ms)
sum(rate(http_request_duration_bucket{le="0.2"}[5m]))
/
sum(rate(http_request_duration_count[5m]))
```

---

## When to Use What

### Decision Tree

```
Problem: Something is wrong!
│
├── Need to know WHAT is wrong?
│   └── → Metrics (dashboards, alerts)
│
├── Need to know WHY it's wrong?
│   └── → Traces (request flow, dependencies)
│
└── Need detailed context?
    └── → Logs (error messages, debug info)
```

### Common Scenarios

| Scenario | Primary Tool | Secondary Tool |
|----------|-------------|----------------|
| "Is the service healthy?" | Metrics | - |
| "Why is latency high?" | Traces | Metrics (to identify when) |
| "Why did this request fail?" | Traces | Logs (for error details) |
| "What happened at 3am?" | Metrics + Logs | Traces (for specific requests) |
| "Is this deployment working?" | Metrics | Traces (if issues found) |

### The Correlation Flow

1. **Alert fires** (Metrics)
2. **Check dashboard** (Metrics) - identify the problem
3. **Click exemplar** (Metrics → Traces) - find slow request
4. **View trace details** (Traces) - understand the flow
5. **Click trace-to-logs** (Traces → Logs) - see error details

This is the "magic" of correlated observability that this demo showcases.

---

## Further Reading

- [Google SRE Book - Monitoring](https://sre.google/sre-book/monitoring-distributed-systems/)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/naming/)
- [Grafana Tempo Documentation](https://grafana.com/docs/tempo/latest/)
