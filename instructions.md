# Project details/expectations

## Goals
Build a **production-grade** .NET 10 API, which demonstrates Open Telemetry based (traces, metrics, logs) and follows Clean Architecture layers. This API is the core backend for a multi-tenant B2B SaaS application. It should be able to demonstrate the ability that we can gain insights into our platform's performance, investigate slowdown/delays and be able to drill down to logs to find out what went wrong or be able to the RCA (Root Cause Analysis).
From day one, this solution should demonstrate how we are going to handle OTel data when the service is horizontally scaled and be able to identify the problematic instance.
The solution should also provide percentile latency visibility (`p50`, `p95`, `p99`), support SLI/SLO reporting, and make uptime/reliability status easy to answer.

---

## Non-negotiables
  - Use .NET SDK and latest C# language features
  - Use Clean Architecture layers
  - Use PostgreSQL as database
  - Use Dapper for any database interaction
  - Use Serilog for structured logging
  - Use Podman to setup the compose stack and service scaling
  - Any code related to OTel in the API must have details of what and why we are doing it in comments
  - Ability to identify slow or failed requests and see the tracing and logs for it
  - Ability to visualize and query latency percentiles (`p50`, `p95`, `p99`) at service and endpoint level
  - Ability to define and report SLI/SLO posture (availability, latency, and error-rate based)
  - Ability to report service uptime and health over time
