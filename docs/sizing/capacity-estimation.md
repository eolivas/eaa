# Capacity Estimation Methodology

This document defines the standard six-step capacity estimation methodology that all services must complete before go-live. It includes reference numbers, a fully worked example for the Orders PoC, architectural decision gates, and the three-scenario scaling projection pattern.

---

## 1. Six-Step Capacity Estimation Methodology

Every new service or major feature must produce a capacity estimate following these six steps.

### Step 1 — Define Load Profile

Identify the fundamental demand parameters:

| Parameter | Description |
|-----------|-------------|
| **DAU** | Daily Active Users |
| **Peak-to-average ratio** | How much peak traffic exceeds average (typically 2×–10×) |
| **Read/write ratio** | Proportion of read vs. write operations (e.g., 95:5) |
| **Requests per user per day** | Average number of requests each active user generates |
| **Average request size** | Payload size for a typical inbound request (bytes) |
| **Average response size** | Payload size for a typical outbound response (bytes) |

### Step 2 — Derive QPS (Queries Per Second)

Calculate average and peak request rates:

```
Total daily requests = DAU × requests_per_user_per_day

Average QPS = total_daily_requests ÷ 86,400

Peak QPS = average_QPS × peak_to_average_ratio
```

Split by operation type:

```
Read QPS (peak) = peak_QPS × read_ratio
Write QPS (peak) = peak_QPS × write_ratio
```

### Step 3 — Estimate Storage

Project storage growth over time:

```
Daily new records = DAU × writes_per_user_per_day

Record size (bytes) = schema_columns_sum + index_overhead

Daily storage growth = daily_new_records × record_size

Annual storage = daily_storage_growth × 365 × replication_factor
```

Include indexes and replicas in the replication factor (typically 2×–3× for PostgreSQL with one read replica and indexes).

### Step 4 — Estimate Bandwidth

Calculate peak network throughput:

```
Peak inbound bandwidth = write_QPS_peak × avg_request_size

Peak outbound bandwidth = read_QPS_peak × avg_response_size

Total peak bandwidth = peak_inbound + peak_outbound
```

### Step 5 — Estimate Latency Budget

Allocate time from the end-to-end SLA to each component:

```
Latency headroom = target_p99_SLA
                   − network_overhead
                   − middleware_overhead (auth, logging, validation)
                   − pipeline_overhead (serialization, routing)
```

The remaining headroom is available for business logic and data-store access. If headroom is insufficient, architectural changes (caching, in-process cache, read replicas) are required.

### Step 6 — Map to Architectural Decisions

Compare the computed metrics (peak QPS, storage growth, latency headroom) against the decision gates table (see §4 below). Any metric that crosses a threshold triggers the corresponding architectural response. Document all triggered gates and their required responses.

---

## 2. Reference Numbers Table

### Latency Reference

| Component | Approximate Latency | Unit |
|-----------|--------------------:|------|
| L1 cache read | ~0.5 | ns |
| L2 cache read | ~7 | ns |
| RAM read | ~100 | ns |
| SSD sequential read (4 KB) | ~0.1 | ms |
| SSD random read | ~0.1 | ms |
| HDD random read | ~10 | ms |
| Redis / in-process cache read | ~0.5 | ms |
| Intra-region network round-trip | ~1 | ms |
| Cross-region network round-trip | ~50–150 | ms |
| PostgreSQL indexed read (warm) | ~1–5 | ms |
| PostgreSQL full table scan (1M rows) | ~500 | ms |
| External HTTP service call (same region) | ~5–20 | ms |

### Throughput Reference

| Component | Approximate Value | Unit |
|-----------|------------------:|------|
| Single Fargate task (2 vCPU / 4 GB) | ~2,000–8,000 | req/s |
| Typical HTTP server (1 vCPU) | ~1,000–5,000 | req/s |
| PostgreSQL reads (indexed) | ~10,000–50,000 | reads/s |
| PostgreSQL writes | ~1,000–5,000 | writes/s |
| Redis throughput | ~100,000 | ops/s |
| SNS/SQS message throughput | ~3,000 | msgs/s |

---

## 3. Worked Example — Orders PoC at 100K DAU

### Step 1 — Load Profile

| Parameter | Value |
|-----------|-------|
| DAU | 100,000 |
| Orders placed per user per day | 0.1 |
| Order reads per user per day | 2.0 |
| Peak-to-average ratio | 5× |
| Read/write ratio | 95:5 |
| Average request size (write) | ~1 KB |
| Average response size (read) | ~2 KB |

### Step 2 — Derive QPS

```
Write requests/day = 100,000 × 0.1 = 10,000
Read requests/day  = 100,000 × 2.0 = 200,000
Total requests/day = 210,000

Average QPS = 210,000 ÷ 86,400 ≈ 2.4 req/s

Peak QPS = 2.4 × 5 ≈ 12 req/s

Read QPS (peak)  = 12 × 0.95 ≈ 11.4 req/s
Write QPS (peak) = 12 × 0.05 ≈ 0.6 req/s
```

### Step 3 — Estimate Storage

```
Daily new orders = 100,000 × 0.1 = 10,000 orders/day

Average record size = ~500 bytes (order row + order_lines + indexes)

Daily storage growth = 10,000 × 500 = 5 MB/day

Annual storage = 5 MB × 365 × 3 (replication + indexes) ≈ 5.5 GB/year
                 ≈ 16 GB (with indexes, WAL, replicas, and overhead)
```

**Result: ~16 GB annual DB storage**

### Step 4 — Estimate Bandwidth

```
Peak inbound  = 0.6 write/s × 1 KB = 0.6 KB/s ≈ 0.005 Mbit/s
Peak outbound = 11.4 read/s × 2 KB = 22.8 KB/s ≈ 0.18 Mbit/s

Total peak bandwidth ≈ 0.2 Mbit/s outbound
```

**Result: ~0.2 Mbit/s peak outbound bandwidth**

### Step 5 — Estimate Latency Budget

```
Target p99 SLA            = 200 ms
Network overhead          =   1 ms  (intra-region)
Middleware overhead       =   5 ms  (auth token validation + logging)
Pipeline overhead         =  13 ms  (serialization, routing, validation)
                            ------
Total fixed overhead      =  19 ms

Latency headroom = 200 − 19 = 181 ms remaining
```

**Result: 181 ms remaining for business logic and data access**

With PostgreSQL indexed reads at 1–5 ms, the Orders service has ample headroom.

### Step 6 — Map to Architectural Decisions

| Metric | Computed Value | Gate Threshold | Triggered? |
|--------|---------------:|---------------:|:----------:|
| Read QPS | 11.4 | ≥ 1,000 | No |
| Write QPS | 0.6 | ≥ 500 | No |
| Annual storage | 16 GB | ≥ 500 GB | No |
| p99 latency remaining | 181 ms | ≤ 50 ms | No |
| Payload size p99 | ~2 KB | ≥ 100 KB | No |

**Result: No architectural decision gates triggered at 100K DAU. A single-instance deployment is sufficient.**

### Summary Table

| Metric | Value |
|--------|-------|
| Peak QPS | ~12 req/s |
| Annual storage | ~16 GB |
| Peak bandwidth | ~0.2 Mbit/s outbound |
| Latency headroom (p99) | 181 ms remaining |
| Compute requirement | 1× Fargate task (0.5 vCPU / 1 GB) |
| DB requirement | 1× RDS instance (db.t3.micro) |

---

## 4. Architectural Decision Gates

When any computed metric crosses a threshold, the corresponding architectural trigger is mandatory. Teams must document the triggered gate and the planned response in their service's capacity estimation.

| # | Metric | Threshold | Architectural Trigger |
|---|--------|-----------|----------------------|
| 1 | Read QPS | ≥ 1,000 | Add caching layer (Redis / ElastiCache) |
| 2 | Read QPS | ≥ 10,000 | Add separate read store (read replica or CQRS projection) |
| 3 | Write QPS | ≥ 500 | Consider async write path (queue + worker) |
| 4 | Write QPS | ≥ 5,000 | Consider DB sharding or partitioned writes |
| 5 | Annual storage | ≥ 500 GB | Plan partitioning strategy or data archiving |
| 6 | p99 latency budget remaining | ≤ 50 ms | Caching mandatory for hot-path reads |
| 7 | p99 latency budget remaining | ≤ 10 ms | In-process cache required (no network hop allowed) |
| 8 | Payload size (p99) | ≥ 100 KB | Add pagination, response streaming, or CDN offload |

---

## 5. Three-Scenario Scaling Projection Pattern

Before go-live, every service must project capacity across three scenarios:

| Scenario | DAU Definition | Purpose |
|----------|---------------|---------|
| **A — Launch** | Expected launch-day DAU | Baseline sizing for initial deployment |
| **B — 12-Month** | Projected DAU at 12 months post-launch | Planned growth infrastructure |
| **C — Worst-Case** | 10× peak launch DAU | Stress-test the architecture |

### Process

1. Run the six-step methodology for each scenario (A, B, C).
2. Compare each scenario's computed metrics against the decision gates table.
3. Document which gates are triggered in each scenario.

### Migration-Path Requirement

**If Scenario C crosses any decision gate threshold**, the architecture must satisfy one of:

- **Option 1 — Handle from day one**: Design and deploy the architecture that satisfies Scenario C requirements at launch.
- **Option 2 — Provide a migration path document** containing:

| Field | Description |
|-------|-------------|
| **Trigger metric** | The specific metric and value that initiates the migration (e.g., "Read QPS crosses 1,000") |
| **Target architecture** | The architectural change required (e.g., "Add Redis caching layer with cache-aside pattern") |
| **Estimated effort** | Engineering effort in person-days to implement the migration |

Teams must not launch a service that would fail under Scenario C without a documented, reviewed migration path.

### Example Projection — Orders PoC

| Metric | Scenario A (100K DAU) | Scenario B (500K DAU) | Scenario C (1M DAU) |
|--------|----------------------:|----------------------:|--------------------:|
| Peak QPS | 12 | 60 | 120 |
| Read QPS (peak) | 11.4 | 57 | 114 |
| Write QPS (peak) | 0.6 | 3 | 6 |
| Annual storage | 16 GB | 80 GB | 160 GB |
| Peak bandwidth | 0.2 Mbit/s | 1.0 Mbit/s | 2.0 Mbit/s |

**Scenario C assessment**: No decision gates triggered even at 10× launch DAU. The Orders PoC architecture (single Fargate task + single RDS instance) scales to 1M DAU without architectural changes. No migration path document required.

---

## References

- [ADR-001 — Clean Architecture](../adr/ADR-001-clean-architecture.md)
- [AWS Topology](../cloud-topology/aws-topology.md)
- [Azure Topology](../cloud-topology/azure-topology.md)
