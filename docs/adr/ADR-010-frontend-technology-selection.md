# ADR-010: Frontend Technology Selection

## Status

Accepted

## Context

The platform requires a single-page application (SPA) frontend for end-user interaction. The frontend must:

- **Integrate with the REST API**: Consume JWT-authenticated endpoints, handle ProblemDetails errors, and display real-time state from TanStack Query caches.
- **Support feature-module architecture**: Teams must be able to work on independent feature modules without conflicts.
- **Maintain type safety**: End-to-end type safety from API contracts through to component props.
- **Fast developer experience**: Hot module replacement (HMR), fast builds, and instant test feedback.
- **Accessible by default**: Components must render accessible HTML with ARIA attributes.
- **Testable**: Unit tests, component tests, and property-based tests must be straightforward.

Alternatives considered:

- **Angular**: Full-featured framework but heavier, opinionated DI system, RxJS complexity for simple CRUD UIs. Better suited for large enterprise teams with Angular expertise.
- **Next.js (React)**: Server-side rendering adds complexity not needed for a SPA behind authentication. The API is already a separate service — SSR provides no SEO benefit for an authenticated app.
- **Vue.js**: Excellent DX but smaller ecosystem for enterprise tooling, fewer TypeScript-first libraries.
- **Svelte/SvelteKit**: Innovative but smaller community, less battle-tested for enterprise, fewer experienced engineers available.
- **Redux (state management)**: Verbose boilerplate for what TanStack Query handles with zero configuration. Redux is appropriate for complex client-side state machines, not server-state caching.
- **Webpack (bundler)**: Slower builds, more complex configuration, less native ESM support compared to Vite.

## Decision

We select the following frontend stack:

| Concern | Library | Why |
|---------|---------|-----|
| **UI Framework** | React 18+ | Largest ecosystem, most hiring availability, hooks-based composition |
| **Build Tool** | Vite 6 | Instant HMR, native ESM, fast production builds, minimal config |
| **Server State** | TanStack Query 5 | Declarative data fetching with caching, deduplication, retry, and stale-while-revalidate |
| **Client State** | Zustand 5 | Minimal API, no boilerplate, works outside React components, tiny bundle |
| **HTTP Client** | Axios | Interceptors for auth tokens and error handling, request/response transforms |
| **Type System** | TypeScript ~5.6 | Full type safety from API types through component props |
| **Testing** | Vitest + @testing-library/react + fast-check | Fast, accessible-query-first, PBT support |
| **Styling** | CSS/Tailwind (project choice) | Not prescribed — teams choose per project |

### Architecture Decisions

- **Feature-module structure**: Each feature lives in `frontend/src/features/{name}/` with its own API hooks, components, types, and barrel exports. Features are independent and composable.
- **Server state vs. client state**: TanStack Query owns all server-derived data (fetching, caching, sync). Zustand owns UI-only state (auth token, theme, form wizard steps). No overlap.
- **No global state management for server data**: Redux/MobX are unnecessary — TanStack Query provides caching, background refetching, and optimistic updates out of the box.
- **Axios over fetch**: Interceptors enable centralized auth token injection, 401 redirect, and network error detection without wrapping every call.
- **Named exports only**: No default exports — improves refactoring safety, IDE auto-imports, and tree-shaking.

## Consequences

### Positive

- **Fast DX**: Vite HMR is near-instant; TanStack Query eliminates manual loading/error state management; Zustand has zero boilerplate.
- **Type safety end-to-end**: TypeScript interfaces mirror backend DTOs — API shape changes are caught at compile time.
- **Minimal bundle size**: Zustand (~1 KB), TanStack Query (~13 KB gzipped), Vite tree-shaking — no framework bloat.
- **Testable**: @testing-library enforces testing from the user's perspective (accessible queries). fast-check provides PBT for component logic.
- **Hiring pool**: React + TypeScript has the largest frontend hiring pool — onboarding new engineers is fast.

### Negative

- **React ecosystem churn**: Libraries evolve rapidly; version upgrades require periodic maintenance.
- **No SSR**: Pure SPA means no server-side rendering. SEO is not a concern (authenticated app), but initial load shows a spinner until JS hydrates.
- **Multiple small libraries vs. framework**: Teams must learn TanStack Query + Zustand + Axios rather than one integrated framework (like Angular's built-in HTTP + state).
- **Accessibility requires discipline**: React doesn't enforce accessibility — developers must follow steering file guidelines (htmlFor, role, aria attributes) manually.
