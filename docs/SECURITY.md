# Security Checklist

This document defines the security checklist for the reference repository. All services derived from this template must satisfy each item before their first production deployment.

---

## OWASP Top 10 (2021) Checklist

Each OWASP Top 10 category must be evaluated. Set the **Status** to `Pass`, `Fail`, or `N/A` and provide relevant notes explaining the assessment rationale.

| Category | Status | Notes |
|---|---|---|
| A01:2021 – Broken Access Control | ☐ Pass / ☐ Fail / ☐ N/A | |
| A02:2021 – Cryptographic Failures | ☐ Pass / ☐ Fail / ☐ N/A | |
| A03:2021 – Injection | ☐ Pass / ☐ Fail / ☐ N/A | |
| A04:2021 – Insecure Design | ☐ Pass / ☐ Fail / ☐ N/A | |
| A05:2021 – Security Misconfiguration | ☐ Pass / ☐ Fail / ☐ N/A | |
| A06:2021 – Vulnerable and Outdated Components | ☐ Pass / ☐ Fail / ☐ N/A | |
| A07:2021 – Identification and Authentication Failures | ☐ Pass / ☐ Fail / ☐ N/A | |
| A08:2021 – Software and Data Integrity Failures | ☐ Pass / ☐ Fail / ☐ N/A | |
| A09:2021 – Security Logging and Monitoring Failures | ☐ Pass / ☐ Fail / ☐ N/A | |
| A10:2021 – Server-Side Request Forgery (SSRF) | ☐ Pass / ☐ Fail / ☐ N/A | |

---

## Secrets Management

### Supported Secret Stores

The following secret stores are supported for production deployments:

- **AWS Secrets Manager**
- **Azure Key Vault**

### Runtime Injection Requirement

Secrets MUST be injected via environment variables or the .NET configuration provider at runtime. Applications must never read secrets from static files bundled into the deployment artifact.

### Prohibition on Committing Secrets

Committing secrets or credentials to source control is **strictly prohibited**. This includes but is not limited to:

- API keys and tokens
- Database connection strings containing passwords
- TLS certificates and private keys
- Service account credentials

Use `.gitignore` rules and pre-commit hooks (e.g., `git-secrets`, `gitleaks`) to prevent accidental commits.

---

## Container Security

### Non-Root User Requirement

All Docker images MUST run as a non-root user. Dockerfiles must include a dedicated application user and switch to it before the `ENTRYPOINT` or `CMD` instruction. Example:

```dockerfile
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser
```

### Weekly Base-Image Rebuild Policy

Each Docker base image MUST be rebuilt at least once per calendar week to incorporate OS-level security patches. CI pipelines should include a scheduled weekly build that pulls the latest base image and publishes an updated container image, even if application code has not changed.

---

## Sign-Off

The following sign-off MUST be completed before the first production deployment of any downstream service derived from this repository.

| Reviewer | Date | Signature |
|---|---|---|
| *(Name of security reviewer)* | *(YYYY-MM-DD)* | *(Approved / Rejected)* |

> **Note:** A named reviewer and date are required. This sign-off confirms that all checklist items above have been evaluated and any findings addressed or accepted with documented risk.
