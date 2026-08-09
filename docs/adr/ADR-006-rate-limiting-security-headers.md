# ADR-006: API Rate Limiting and Security Headers

## Status

Accepted

## Context

The API exposes public-facing HTTP endpoints that need protection against abuse (brute-force attacks, resource exhaustion) and common web security vulnerabilities (clickjacking, MIME sniffing, server fingerprinting). While the MCP layer has its own rate limiting, the standard REST endpoints under `/api/{entities}` had no throttling or security header enforcement.

OWASP guidelines recommend several response headers as baseline security controls. Without rate limiting, a single client can monopolize server resources and degrade service for others.

## Decision

We implement two middleware components in the API pipeline:

### Rate Limiting

- **Algorithm**: ASP.NET Core 8 built-in fixed-window rate limiter (`Microsoft.AspNetCore.RateLimiting`).
- **Scope**: Applied to the `/api/{entities}` endpoint group via `.RequireRateLimiting("{solution-name}-api")`.
- **Partition key**: Authenticated user's `sub` claim; falls back to `RemoteIpAddress` for unauthenticated requests.
- **Configuration**: `RateLimit:PermitLimit` (default 100) and `RateLimit:WindowSeconds` (default 60) read from `IConfiguration`.
- **Response on rejection**: HTTP 429 with `Retry-After` header (seconds remaining in current window).
- **Response on success**: `X-RateLimit-Limit` and `X-RateLimit-Remaining` headers on every response within the window.

### Security Headers

- **`X-Content-Type-Options: nosniff`** — Prevents MIME-sniffing attacks. Added on every response.
- **`X-Frame-Options: DENY`** — Prevents clickjacking via iframe embedding. Added on every response.
- **`Server` header removal** — Prevents server fingerprinting. Removed on every response.
- **`Strict-Transport-Security`** — HSTS with `max-age=31536000; includeSubDomains`, added only when `context.Request.IsHttps` is true (not added over plain HTTP).

### Middleware Ordering

Security headers and correlation ID middleware execute first (before exception handling and CORS) to ensure they are present on all responses including error responses:

```
SecurityHeaders → CorrelationId → ExceptionHandler → CORS → RateLimiting → Auth → Routing
```

## Consequences

### Positive

- **Abuse protection**: Prevents a single client from overwhelming the API with requests.
- **OWASP compliance**: Satisfies baseline security header recommendations without requiring a reverse proxy.
- **Configuration-driven**: Per-environment tuning without code changes (higher limits for production, lower for staging).
- **Transparent to clients**: Rate limit headers inform well-behaved clients of their quota status.

### Negative

- **Fixed-window limitations**: A burst at the window boundary can allow up to 2× the limit in a short period. Sliding window or token bucket algorithms provide smoother throttling but add complexity.
- **IP-based fallback**: Shared NAT/proxy scenarios may unfairly throttle multiple users behind the same IP.
- **No distributed state**: Rate limit state is per-instance (in-memory). In a multi-instance deployment, a distributed store (Redis) would be needed for accurate cross-instance limiting.
