# Branch Protection Rules

This document defines the branch protection rules enforced on the `main` branch of this repository.

---

## Protected Branch: `main`

### Required Reviews

| Setting | Value |
|---|---|
| Required approving reviews | 1 |
| Dismiss stale reviews on new commits | Enabled |

When a new commit is pushed to a pull request that already has an approving review, the existing approval is automatically dismissed. This ensures that every merged change has been reviewed in its final form.

### Required Status Checks

All of the following status checks must pass before a pull request can be merged into `main`:

| Status Check | Description |
|---|---|
| `build` | Solution compiles with zero errors and zero elevated warnings |
| `unit-tests` | All unit test projects pass with zero failures |
| `architecture-tests` | NetArchTest dependency-direction rules pass |
| `lint` | Code style and static analysis checks pass |

**Branches must be up to date before merging.** The pull request branch must be rebased or merged with the latest `main` before the merge is permitted. This prevents broken builds caused by incompatible changes that individually pass CI but conflict when combined.

### Commit Signing

| Setting | Value |
|---|---|
| Require signed commits | Enabled |

All commits merged into `main` must be cryptographically signed (GPG or SSH). Unsigned commits will be rejected.

### Push Restrictions

Direct pushes to `main` are restricted. Only the following identities may push directly:

| Identity | Type | Justification |
|---|---|---|
| GitHub Actions service account | Service account | Automated release tagging and version bumps |
| Repository admins | User role | Emergency hotfixes and administrative operations |

All other contributors must use pull requests that satisfy the review, status check, and signing requirements documented above.

---

## Enforcement Summary

```
main branch protection:
  ├── Reviews
  │   ├── Required approving reviews: 1
  │   └── Dismiss stale reviews on new push: ✓
  ├── Status Checks (required, must be up to date)
  │   ├── build
  │   ├── unit-tests
  │   ├── architecture-tests
  │   └── lint
  ├── Commit Signing
  │   └── Require signed commits: ✓
  └── Push Restrictions
      ├── Allow: GitHub Actions service account
      ├── Allow: Repository admins
      └── Deny: All others (must use PR)
```

---

## Applying These Rules

These rules are configured in the repository's **Settings → Branches → Branch protection rules** for the `main` branch pattern. When creating a new service repository from this template, apply the same rules via the GitHub API or Terraform/Pulumi IaC to ensure consistency across all repositories.
