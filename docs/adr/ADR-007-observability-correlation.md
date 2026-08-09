# ADR-007: Observability Stack and Correlation ID Propagation

## Status

Accepted

## Context

The template documented OpenTelemetry tracing in ADR-005 and the README, but the implementation was incomplete:

1. **Metrics** were not exported — only traces were configured via `WithTracing`. Application-specific counters (outbox processed/failed, MCP token usage) and HTTP request metrics had no export path.
2. **Local backends** were missing — developers had no way to visualize traces or query metrics locally. They had to read raw logs or connect to a cloud provider's observability stack.
3. **Correlation** across boundaries was fragile — HTTP requests generated a trace ID, but there was no explicit correlation ID that traveled from the HTTP request through the outbox and into MassTransit message consumers. Log entries across these boundaries could not be easily joined.

## Decision

### OpenTelemetry Metrics

Add `WithMetrics` to the existing OpenTelemetry builder registration:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tp => tp.AddAspNetCoreInstrumentation().AddEntityFrameworkCoreInstrumentation().AddOtlpExporter())
    .WithMetrics(mp => mp.AddAspNetCoreInstrumentation().AddMeter("{SolutionName}.Metrics").AddOtlpExporter());
```

Both traces and metrics are exported to the OTEL Collector via OTLP gRPC (port 4317). If the collector is unreachable, the exporter drops data silently (non-fatal — the API continues serving requests).

### Local Observability Stack (Docker Compose)

Add three services to `docker-compose.yml`:

| Service | Image | Port | Purpose |
|---------|-------|------|---------|
| `otel-collector` | `otel/opentelemetry-collector-contrib:0.96.0` | 4317 (gRPC) | Receives OTLP data, forwards traces to Jaeger and metrics to Prometheus |
| `jaeger` | `jaegertracing/all-in-one:1.54` | 16686 | Distributed trace visualization |
| `prometheus` | `prom/prometheus:v2.50.0` | 9090 | Metrics query and dashboard |

The API depends on `otel-collector` with `condition: service_healthy` to ensure the collector is ready before the API starts exporting.

### Correlation ID Propagation

Implement a `CorrelationIdMiddleware` that:

1. Extracts `X-Correlation-Id` from the request header (must be a valid GUID).
2. If missing/invalid, generates a new GUID.
3. Pushes a `CorrelationId` property to the Serilog `LogContext` for the request duration.
4. Sets `X-Correlation-Id` on the response header via `OnStarting` callback.

The correlation ID is then propagated through the system:

- **Outbox**: The `{SolutionName}DbContext` captures the current correlation ID from `ICorrelationIdAccessor` and stores it on the `OutboxMessage.CorrelationId` column.
- **Outbox Processor**: When publishing, includes the correlation ID in MassTransit message headers (`X-Correlation-Id`).
- **MassTransit Consumers**: Extract the correlation ID from the message header and push it to the Serilog LogContext.

This creates an unbroken correlation chain: HTTP request → log entries → outbox row → published message → consumer log entries.

## Consequences

### Positive

- **Full local observability**: Developers can inspect traces (Jaeger) and query metrics (Prometheus) without any cloud account or external tool configuration.
- **End-to-end correlation**: A single `X-Correlation-Id` value connects all log entries and spans from HTTP ingress through async message processing.
- **Metrics parity**: Application metrics (outbox throughput, MCP token usage, request rates) are now queryable alongside framework metrics.
- **Non-intrusive**: Telemetry export failure does not impact API availability or correctness.

### Negative

- **Docker Compose resource usage**: Three additional containers increase local memory and CPU usage (~300-500 MB combined). Developers on constrained machines may need to disable them.
- **No persistence**: Jaeger and Prometheus use in-memory/ephemeral storage. Traces and metrics are lost when containers restart.
- **OTEL Collector version pinning**: The collector image version must be manually updated to receive bug fixes and new exporters.
