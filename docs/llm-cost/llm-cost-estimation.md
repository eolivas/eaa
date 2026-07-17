# Monthly LLM Cost Estimation Methodology

## Overview

This document applies the §1.7 back-of-the-envelope estimation methodology to LLM token volumes consumed by the MCP Gateway. The goal is to project monthly LLM API costs at different scale points, determine when cost thresholds are breached, and identify the mitigation levers available.

The same six-step approach used for infrastructure capacity estimation is adapted here:

1. **Define the Token Load Profile** — establish per-user MCP tool call patterns and token consumption
2. **Derive Token Throughput** — convert daily volumes to tokens/second and monthly totals
3. **Estimate Monthly Cost** — apply per-token pricing by model tier
4. **Apply Caching Reduction** — model the impact of semantic caching on billable calls
5. **Compute Cost-per-DAU** — derive the unit economics metric
6. **Map to Cost Decision Gates** — trigger mitigation actions when thresholds are exceeded

---

## Six-Step LLM Cost Estimation

### Step 1: Define the Token Load Profile

Establish baseline MCP tool usage assumptions per user per day.

**Load profile parameters:**

| Parameter | Value | Rationale |
|---|---|---|
| MCP tool calls per user per day | 15 | Mix of lookups, status checks, and occasional complex queries |
| Tier distribution | 70% Lightweight / 20% Standard / 10% Heavy | Most calls are simple lookups (get_order); some require moderate reasoning; few need complex generation |
| Avg input tokens per call (Lightweight) | 2,000 | System (500) + schemas (500) + history (500) + margin (500) |
| Avg output tokens per call (Lightweight) | 500 | Short factual responses |
| Avg input tokens per call (Standard) | 5,000 | System (500) + schemas (500) + history (2,000) + result (1,500) + margin (500) |
| Avg output tokens per call (Standard) | 1,500 | Moderate explanations |
| Avg input tokens per call (Heavy) | 8,000 | Full context budget utilised |
| Avg output tokens per call (Heavy) | 4,000 | Complex multi-step reasoning output |
| Peak-to-average ratio | 3× | Business-hours concentration |
| Rate limit | 50 calls/user/hour | Hard cap enforced by MCP Gateway |

**Per-user daily token breakdown:**
```
Lightweight calls: 15 × 0.70 = 10.5 calls/user/day
  Input tokens:  10.5 × 2,000 = 21,000 tokens
  Output tokens: 10.5 × 500   =  5,250 tokens

Standard calls: 15 × 0.20 = 3.0 calls/user/day
  Input tokens:  3.0 × 5,000 = 15,000 tokens
  Output tokens: 3.0 × 1,500 =  4,500 tokens

Heavy calls: 15 × 0.10 = 1.5 calls/user/day
  Input tokens:  1.5 × 8,000 = 12,000 tokens
  Output tokens: 1.5 × 4,000 =  6,000 tokens
```

**Per-user daily totals:**
```
Total input tokens/user/day  = 21,000 + 15,000 + 12,000 = 48,000
Total output tokens/user/day =  5,250 +  4,500 +  6,000 = 15,750
Total tokens/user/day        = 63,750
```

---

### Step 2: Derive Token Throughput

Convert per-user volumes to platform-wide daily and monthly totals.

**Formulas:**
```
Daily input tokens  = DAU × input_tokens_per_user_per_day
Daily output tokens = DAU × output_tokens_per_user_per_day
Monthly tokens      = daily_tokens × 30
```

**Throughput at scale:**

| DAU | Daily Input Tokens | Daily Output Tokens | Monthly Input Tokens | Monthly Output Tokens |
|---|---|---|---|---|
| 10K | 480M | 157.5M | 14.4B | 4.73B |
| 100K | 4.8B | 1.575B | 144B | 47.3B |
| 1M | 48B | 15.75B | 1,440B | 473B |

---

### Step 3: Estimate Monthly Cost (Without Caching)

Apply per-token pricing by model tier.

**Token pricing (per 1M tokens):**

| Model Tier | Input Price | Output Price | Example Models |
|---|---|---|---|
| Lightweight | $0.15 | $0.60 | GPT-4o-mini, Claude Haiku, Gemini Flash |
| Standard | $3.00 | $15.00 | GPT-4o, Claude Sonnet, Gemini Pro |
| Heavy | $15.00 | $60.00 | GPT-4.5, Claude Opus, o1/o3 reasoning |

**Monthly cost formula per tier:**
```
Monthly cost (tier) = (monthly_input_tokens × input_price / 1M)
                    + (monthly_output_tokens × output_price / 1M)
```

**Worked example at 100K DAU (no caching):**

```
Lightweight (70% of calls):
  Monthly input:  100,000 × 21,000 × 30 = 63B tokens
  Monthly output: 100,000 ×  5,250 × 30 = 15.75B tokens
  Cost: (63,000 × $0.15) + (15,750 × $0.60) = $9,450 + $9,450 = $18,900

Standard (20% of calls):
  Monthly input:  100,000 × 15,000 × 30 = 45B tokens
  Monthly output: 100,000 ×  4,500 × 30 = 13.5B tokens
  Cost: (45,000 × $3.00) + (13,500 × $15.00) = $135,000 + $202,500 = $337,500

Heavy (10% of calls):
  Monthly input:  100,000 × 12,000 × 30 = 36B tokens
  Monthly output: 100,000 ×  6,000 × 30 = 18B tokens
  Cost: (36,000 × $15.00) + (18,000 × $60.00) = $540,000 + $1,080,000 = $1,620,000

Total monthly (no caching): $18,900 + $337,500 + $1,620,000 = $1,976,400
```

---

### Step 4: Apply Caching Reduction

The MCP Gateway's semantic cache (SHA-256 keyed by `toolName + JSON-serialised arguments`) eliminates redundant LLM calls for repeated queries.

**Caching assumptions:**

| Cache Category | TTL | Hit Rate (read-heavy workload) | Rationale |
|---|---|---|---|
| Reference data | 3,600 s | 80% | High repetition (product info, config) |
| Entity state | 30 s | 40% | Moderate repetition (order status checks) |
| Aggregation | 300 s | 70% | Dashboard/report queries repeated across users |

**Blended cache hit rate calculation:**

Assuming workload distribution: 40% reference, 35% entity, 25% aggregation:
```
Blended hit rate = (0.40 × 0.80) + (0.35 × 0.40) + (0.25 × 0.70)
                 = 0.32 + 0.14 + 0.175
                 = 0.635 ≈ 65%
```

**Effective billable call reduction:** ~65% of calls are served from cache and incur zero LLM tokens.

**Remaining billable fraction:** 35% of original volume.

---

### Step 5: Compute Cost-per-DAU

**Formula:**
```
Cost per DAU per day = monthly_cost ÷ 30 ÷ DAU
```

---

### Step 6: Map to Cost Decision Gates

**Primary threshold:** $0.01 per DAU per day

When estimated LLM cost per DAU exceeds $0.01/day, the following mitigations MUST be evaluated:

1. **Aggressive caching** — extend TTLs, expand cache coverage to additional tool categories
2. **Stricter tier enforcement** — reclassify tools to push more calls to the Lightweight tier
3. **Fine-tuned model deployment** — deploy task-specific fine-tuned models at lower per-token cost

---

## Cost Scaling Table

### Without Caching

| DAU | Monthly LLM Cost | Cost/DAU/Day | Threshold Exceeded? |
|---|---|---|---|
| 10K | $197,640 | $0.659 | **Yes** |
| 100K | $1,976,400 | $0.659 | **Yes** |
| 1M | $19,764,000 | $0.659 | **Yes** |

### With Caching (65% hit rate → 35% billable)

| DAU | Monthly LLM Cost | Cost/DAU/Day | Threshold Exceeded? |
|---|---|---|---|
| 10K | $69,174 | $0.231 | **Yes** |
| 100K | $691,740 | $0.231 | **Yes** |
| 1M | $6,917,400 | $0.231 | **Yes** |

### With Caching + Aggressive Tier Shift (90% Lightweight / 8% Standard / 2% Heavy)

Applying both caching (65% reduction) and tier reclassification:

```
Per-user daily tokens (after tier shift):
  Lightweight: 15 × 0.90 = 13.5 calls → 27,000 input + 6,750 output
  Standard:    15 × 0.08 = 1.2 calls  →  6,000 input + 1,800 output
  Heavy:       15 × 0.02 = 0.3 calls  →  2,400 input + 1,200 output

Monthly cost at 100K DAU (35% billable after cache):
  Lightweight: (27,000 × 100K × 30 × 0.35) × ($0.15/1M) input
             + (6,750 × 100K × 30 × 0.35) × ($0.60/1M) output
             = 28.35B × $0.15/1M + 7.09B × $0.60/1M
             = $4,253 + $4,253 = $8,505

  Standard:   (6,000 × 100K × 30 × 0.35) × ($3.00/1M) input
            + (1,800 × 100K × 30 × 0.35) × ($15.00/1M) output
            = 6.3B × $3.00/1M + 1.89B × $15.00/1M
            = $18,900 + $28,350 = $47,250

  Heavy:      (2,400 × 100K × 30 × 0.35) × ($15.00/1M) input
            + (1,200 × 100K × 30 × 0.35) × ($60.00/1M) output
            = 2.52B × $15.00/1M + 1.26B × $60.00/1M
            = $37,800 + $75,600 = $113,400

  Total: $8,505 + $47,250 + $113,400 = $169,155
  Cost/DAU/Day: $169,155 ÷ 30 ÷ 100,000 = $0.056
```

| DAU | Monthly LLM Cost | Cost/DAU/Day | Threshold Exceeded? |
|---|---|---|---|
| 10K | $16,916 | $0.056 | **Yes** |
| 100K | $169,155 | $0.056 | **Yes** |
| 1M | $1,691,550 | $0.056 | **Yes** |

### With All Mitigations (Cache + Tier Shift + Fine-tuned Models at 80% cost reduction)

Fine-tuned models reduce effective per-token cost by ~80% for domain-specific tasks:

| DAU | Monthly LLM Cost | Cost/DAU/Day | Threshold Exceeded? |
|---|---|---|---|
| 10K | $3,383 | $0.011 | **Yes** (marginal) |
| 100K | $33,831 | $0.011 | **Yes** (marginal) |
| 1M | $338,310 | $0.011 | **Yes** (marginal) |

---

## Summary Comparison Table

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────┐
│ LLM Cost Estimation — Monthly Cost Projection                                                    │
├──────────────────────────────────────┬──────────────┬──────────────┬──────────────┬─────────────┤
│ Configuration                        │ 10K DAU      │ 100K DAU     │ 1M DAU       │ $/DAU/Day   │
├──────────────────────────────────────┼──────────────┼──────────────┼──────────────┼─────────────┤
│ No caching (baseline)                │ $197,640     │ $1,976,400   │ $19,764,000  │ $0.659      │
│ With caching (65% hit rate)          │ $69,174      │ $691,740     │ $6,917,400   │ $0.231      │
│ Cache + aggressive tier shift        │ $16,916      │ $169,155     │ $1,691,550   │ $0.056      │
│ Cache + tier shift + fine-tuned      │ $3,383       │ $33,831      │ $338,310     │ $0.011      │
├──────────────────────────────────────┼──────────────┼──────────────┼──────────────┼─────────────┤
│ Target threshold                     │ —            │ —            │ —            │ ≤ $0.010    │
└──────────────────────────────────────┴──────────────┴──────────────┴──────────────┴─────────────┘
```

---

## Cost-per-DAU Threshold Statement

> **When the estimated LLM cost per DAU exceeds $0.01 per day, the following mitigation options MUST be evaluated before proceeding to production:**
>
> 1. **Aggressive caching** — increase semantic cache TTLs beyond default values (e.g., reference data to 7,200 s, aggregation to 600 s), expand cache coverage to entity-state tools where staleness is acceptable, target ≥80% blended cache hit rate
>
> 2. **Stricter tier enforcement** — reclassify tools currently routed to Standard or Heavy tiers; push ≥90% of tool calls to the Lightweight tier; reserve Heavy tier exclusively for multi-step reasoning tasks that demonstrably require it
>
> 3. **Fine-tuned model deployment** — train domain-specific fine-tuned models on production tool-call traces; deploy as dedicated inference endpoints; target ≥80% per-token cost reduction compared to general-purpose models while maintaining equivalent task accuracy

The $0.01/DAU/day threshold represents the point at which LLM API costs become a material line item (>$300K/month at 1M DAU). Below this threshold, LLM costs are proportionate to the value delivered. Above it, unit economics degrade and alternative approaches must be explored.

---

## Key Observations

1. **Heavy-tier calls dominate cost** — at only 10% of call volume, Heavy-tier models account for ~82% of total spend due to 100× pricing differential vs. Lightweight.

2. **Caching alone is insufficient** — even with a 65% cache hit rate, cost/DAU/day remains $0.231, which is 23× above the threshold.

3. **Tier enforcement is the highest-leverage mitigation** — shifting from 70/20/10 to 90/8/2 tier distribution reduces cost by ~75% because it eliminates most Standard and Heavy calls.

4. **Fine-tuned models unlock sustainable unit economics** — only the combination of all three mitigations approaches the $0.01/DAU/day target.

5. **Cost scales linearly with DAU** — there are no economies of scale in per-token LLM pricing; cost/DAU/day is constant regardless of DAU count. Volume discounts from providers may provide 10-30% reduction at scale but do not change the order of magnitude.

---

## Methodology Notes

- Token counts are estimates based on the MCP Gateway's 8,000-token per-call context budget (500 system + 500 schemas + 2,000 history + 4,000 result + 1,000 margin)
- Pricing reflects publicly available API rates as of 2024; actual negotiated enterprise rates may differ
- The six-step methodology mirrors §1.7 (capacity estimation) adapted from infrastructure sizing to token-volume costing
- All calculations assume 30-day months for simplicity
- Cache hit rates are conservative estimates for read-heavy enterprise workloads; actual rates depend on query diversity and access patterns
