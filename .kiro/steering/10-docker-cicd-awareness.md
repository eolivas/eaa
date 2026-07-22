---
inclusion: auto
---

# Docker & CI/CD Awareness

## Docker Compose Services

Local development uses `docker-compose.yml` with these services:

| Service | Image | Ports | Purpose |
|---------|-------|-------|---------|
| `orders-api` | Built from `src/Orders.Api/Dockerfile` | 5000:8080 | .NET 8 API |
| `postgres` | `postgres:16` | 5432:5432 | PostgreSQL database |
| `rabbitmq` | `rabbitmq:3.13-management` | 5672, 15672 | Message broker |
| `frontend` | Built from `frontend/Dockerfile` | 3000:80 | React SPA (nginx) |

### Service Dependencies
- `orders-api` depends on `postgres` (healthy) and `rabbitmq` (healthy)
- `frontend` depends on `orders-api`

### Connection Strings (Development)
- PostgreSQL: `Host=postgres;Port=5432;Database=orders;Username=postgres;Password=postgres`
- RabbitMQ: `Host=rabbitmq;Username=guest;Password=guest`

### Adding a New Service
1. Add service definition to `docker-compose.yml`
2. Add health check
3. Wire dependencies with `depends_on: condition: service_healthy`
4. Expose ports only if needed for local development

## CI/CD Pipeline (`.github/workflows/ci.yml`)

### Backend Pipeline Stages

```
lint-and-test → build-and-push → deploy-staging → deploy-production
```

1. **Lint & Test** (all PRs and main pushes)
 - Restore → Build Release → Test with coverage
 - Publish test results (dorny/test-reporter)
 - Upload coverage to Codecov
 - **Coverage threshold: 80%** (fails CI if below)
 - SonarCloud analysis

2. **Build & Push** (main branch only)
 - Docker multi-arch build (amd64 + arm64)
 - Push to Amazon ECR
 - Trivy vulnerability scan (CRITICAL/HIGH fails the build)
 - SARIF upload to GitHub Security

3. **Deploy Staging**
 - ECS Fargate deployment
 - Smoke test: `GET /health` with retries
 - Environment: `staging`

4. **Deploy Production**
 - ECS Fargate deployment
 - Git tag: `release-{short-sha}`
 - Environment: `production` (requires approval)

### Frontend Pipeline (`.github/workflows/frontend-ci.yml`)

```
lint → type-check → test → build → deploy (S3 + CloudFront)
```

- Triggers on pushes to `main` when `frontend/**` changes
- Node 20, npm ci
- Vitest with coverage
- Vite production build
- S3 sync + CloudFront invalidation

## Infrastructure

- **Cloud**: AWS
- **Compute**: ECS Fargate
- **Container Registry**: Amazon ECR
- **Database**: PostgreSQL (RDS in production, container locally)
- **Messaging**: RabbitMQ (Amazon MQ or self-managed in production)
- **Frontend Hosting**: S3 + CloudFront
- **Auth**: OIDC for CI/CD, JWT Bearer for API

## Key Environment Variables

| Variable | Where | Purpose |
|----------|-------|---------|
| `ConnectionStrings__OrdersDb` | API container | PostgreSQL connection |
| `RabbitMq__Host/Username/Password` | API container | Message broker |
| `Jwt__Authority` | API container | OIDC provider URL |
| `Jwt__Audience` | API container | Expected JWT audience |
| `ASPNETCORE_ENVIRONMENT` | API container | Environment name |

## When Modifying Infrastructure

- Update `docker-compose.yml` for local dev changes
- Update `src/Orders.Api/Dockerfile` for API container changes
- Update `frontend/Dockerfile` for frontend container changes
- Update `.github/workflows/ci.yml` for backend CI/CD changes
- Update `.github/workflows/frontend-ci.yml` for frontend CI/CD changes
- New services need: Dockerfile, docker-compose entry, CI workflow updates, ECS task definition updates
