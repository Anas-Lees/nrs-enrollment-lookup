# NRS Enrollment — Applicant Lookup

[![CI](https://github.com/Anas-Lees/nrs-enrollment-lookup/actions/workflows/ci.yml/badge.svg)](https://github.com/Anas-Lees/nrs-enrollment-lookup/actions/workflows/ci.yml)
[![CD](https://github.com/Anas-Lees/nrs-enrollment-lookup/actions/workflows/cd.yml/badge.svg)](https://github.com/Anas-Lees/nrs-enrollment-lookup/actions/workflows/cd.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> Royal Oman Police · National Registration System (NRS) Modernisation
> **Enrollment track · Applicant Lookup feature** — a proof-of-concept built the way the production module will be built.

The Applicant Lookup screen is the entry point of the NRS Enrollment module. At any of the
58+ domestic sites or 75+ embassies, an operator searches for a person, opens their record,
and decides whether to start a new application or continue an existing one.

This repository delivers that feature as a clean, layered, **bilingual (Arabic / English)**,
production-shaped slice: a documented REST API over EF Core and an Angular single-page app,
with tests, containers, CI/CD and OpenShift manifests.

---

## Screenshots

**Sign in — Keycloak login themed to match the console**

![NRS-themed Keycloak login page](docs/screenshots/login.png)

**Smart search — card results with the live quick-preview panel (English)**

![Operator console: smart search with card results and quick-preview, English](docs/screenshots/search-en.png)

**Full Arabic UI — mirrored right-to-left, Arabic-Indic dates**

![Operator console in Arabic, right-to-left](docs/screenshots/search-ar.png)

**Applicant profile — summary, biographic details & documents**

![Applicant profile page](docs/screenshots/profile.png)

**API reference (Scalar) — every endpoint documented from the OpenAPI contract**

![Scalar API reference rendered from the OpenAPI contract](docs/screenshots/scalar.png)

> Data is synthetic (100 generated persons). Photos are initials-avatar placeholders — no real
> people are depicted.

---

## Architecture

How the pieces talk to each other at runtime:

```mermaid
flowchart LR
  browser["Operator browser"]

  subgraph SPA["Angular SPA · nginx"]
    app["UI · services · DTOs"]
  end

  subgraph API["ASP.NET Core API · .NET 10"]
    ctrl["Thin controllers"]
    svc["PersonLookupService"]
    repo["Repository · EF Core"]
  end

  oracle[("Oracle")]
  redis[("Redis cache")]
  kc["Keycloak · OIDC"]
  otel["OTel collector"]

  browser --> SPA
  SPA -->|"/api proxy · bearer JWT"| ctrl
  ctrl --> svc
  svc --> repo
  repo -->|"SQL"| oracle
  svc -.->|"cache-aside"| redis
  SPA -.->|"login · OIDC + PKCE"| kc
  API -.->|"validate JWT"| kc
  API -.->|"telemetry"| otel

  classDef client fill:#1f6feb,stroke:#0b3d91,color:#fff;
  classDef api fill:#1c6b41,stroke:#0f3d24,color:#fff;
  classDef store fill:#b8860b,stroke:#7a5a08,color:#fff;
  classDef ext fill:#6f42c1,stroke:#3f2384,color:#fff;
  class browser,app client;
  class ctrl,svc,repo api;
  class oracle,redis store;
  class kc,otel ext;
```

- The SPA is served by nginx, which also reverse-proxies `/api` to the backend, so the browser
  talks to a single origin (no CORS in the container stack).
- The SPA logs the operator in directly against **Keycloak** using Authorization Code + PKCE
  and attaches the resulting bearer token to every API call.
- The API **validates** each JWT against Keycloak (issuer, audience, lifetime, signing key) and
  reads from **Oracle** through EF Core, with **Redis** as a best-effort cache-aside in front of
  hot profile reads.
- OpenTelemetry is instrumented in the API; when an OTLP endpoint is configured it exports
  traces and metrics to a collector (otherwise it is a no-op).

Clean, layered architecture with dependencies pointing **inward**
(`Api → Application → Domain`; `Infrastructure` wired at the composition root) — see
[ADR 0002](docs/adr/0002-layered-architecture.md). The
[OpenAPI contract](docs/api/openapi.yaml) is the single source of truth, enforced by contract
tests so code and spec can't drift.

### A search request, end to end

```mermaid
%%{init: {"theme":"base","themeVariables":{"actorBkg":"#1c6b41","actorBorder":"#0f3d24","actorTextColor":"#ffffff","actorLineColor":"#9bb5a6"}}}%%
sequenceDiagram
  autonumber
  actor O as Operator
  participant S as Angular SPA
  participant A as API controller
  participant V as PersonLookupService
  participant R as PersonRepository
  participant DB as Database
  O->>S: Enter filters, click Search
  S->>A: GET /api/v1/persons/search
  A->>V: SearchAsync(criteria)
  V->>R: SearchAsync with normalised paging
  R->>DB: SELECT with WHERE, OFFSET and LIMIT
  DB-->>R: Rows and total count
  R-->>V: Entities and total
  V-->>A: PagedResult of PersonSummary
  A-->>S: 200 application/json
  S-->>O: Paginated result cards
```

---

## Tech stack

| Layer        | Technology                                                            |
| ------------ | -------------------------------------------------------------------- |
| Frontend     | Angular 21 (standalone components, signals, reactive forms, router)  |
| Backend      | ASP.NET Core (.NET 10) — clean layered architecture                  |
| Data access  | Entity Framework Core (SQLite for dev, Oracle for prod)              |
| API docs     | OpenAPI (contract-first) rendered with Scalar                        |
| Identity     | Keycloak (OIDC / JWT) — feature-flagged                              |
| Quality      | xUnit, Playwright; unit · integration · contract · architecture · e2e |
| Platform     | Docker, GitHub Actions (CI/CD), OpenShift                            |

---

## Repository layout

```
nrs-enrollment-lookup/
├── backend/      ASP.NET Core solution (Api · Application · Domain · Infrastructure + tests)
├── frontend/     Angular 21 single-page app (+ Playwright e2e)
├── deploy/       OpenShift manifests + Keycloak realm
├── docs/         ADRs, the frozen OpenAPI contract, diagram source, screenshots, onboarding
└── .github/      CI/CD workflows and repo policy
```

The full target tree (every file tagged with its workstream) lives in
[`docs/project-structure.md`](docs/project-structure.md).

---

## Getting started

### Option A — Docker (the full production-shaped stack)

One command runs everything: Angular SPA → API on **Oracle**, with **Keycloak** login.

```bash
docker compose up --build
```

- App: **http://localhost:4200** — redirects to Keycloak to log in (**operator1 / operator1**)
- API docs (Scalar): http://localhost:5000/scalar · Keycloak admin: http://localhost:8081 (admin / admin)

First start takes a few minutes (Oracle initialises; the API waits for it, then creates the
schema and seeds 100 persons). Auth is enabled for the container SPA via a mounted
`config.json` (see `deploy/spa-config.docker.json`); the image's default is auth-off.

> If `localhost:4200` shows stale content, make sure no host `ng serve` is still bound to
> `:4200` (it shadows Docker on `localhost`). Stop it, or use Option B for host dev.

### Option B — run locally (lightweight: SQLite, no auth)

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/), [Node.js 22+](https://nodejs.org/).

```bash
# backend
cd backend
dotnet run --project src/Nrs.Api          # https://localhost:7001/scalar

# frontend (separate terminal)
cd frontend
npm install
npm start                                  # http://localhost:4200 (proxies /api → backend)
```

The API auto-creates and seeds a SQLite database (100 persons, each with ID cards + passports)
on first run in Development.

### API endpoints

| Method & path | Purpose |
| ------------- | ------- |
| `GET /api/v1/persons/search?crn&name&dob&nationality&page&pageSize` | Paged, multi-filter search (partial bilingual name match) |
| `GET /api/v1/persons/{crn}` | Full profile incl. ID cards + passports |
| `GET /api/v1/audit/recent` | Recent audit-trail entries (operator-only) |
| `GET /health/live` · `/health/ready` · `/health` | Liveness · readiness (DB) · full health |

---

## Testing

```bash
cd backend  && dotnet test     # 80+ tests across unit · integration · contract · architecture
cd frontend && npm run e2e     # 5 Playwright tests: operator journey, filters, Arabic/RTL
```

| Suite | Count | Covers |
| ----- | ----- | ------ |
| Unit (`Nrs.Application.Tests`) | 12 | service orchestration, paging clamps, mapping, audit-safe cache decorator |
| Integration (`Nrs.Api.IntegrationTests`) | 51 | real HTTP → EF Core → SQLite; auth/JWT, audit trail, rate limiting, correlation id, error contract, production-mode & seed-safety (1 live-Oracle test skipped without a DB) |
| Contract (`Nrs.Contract.Tests`) | 14 | code matches `openapi.yaml` (paths, enums, DTOs) |
| Architecture (`Nrs.Architecture.Tests`) | 4 | layering rules enforced |
| E2E (Playwright) | 5 | search → profile; nationality filter; advanced AND filters; Start New Enrollment; Arabic/RTL |

CI runs all of these on every push/PR (plus code coverage and a vulnerable-package gate);
a separate CodeQL workflow runs SAST. CD builds and publishes container images to GHCR with a
Trivy image scan and an SPDX SBOM. (On this private repo, scan results are uploaded as build
artifacts; they would surface in the Security tab once GitHub Advanced Security is enabled.)

---

## Authentication (Keycloak) — feature-flagged

Off by default so the POC runs open. To enable: set `Auth:Enabled=true` (API) and
`environment.auth.enabled=true` (SPA) with a running Keycloak using
[`deploy/keycloak/realm-export.json`](deploy/keycloak/realm-export.json). The API then validates
the JWT on every request (except `/health`) and the SPA guards its routes and attaches the token.

---

## Production readiness

The application code is built secure-by-default, but the Docker Compose and OpenShift samples
deliberately use convenient demo values (inline passwords, open API docs, SQLite). Before any
real deployment, work through [`docs/PRODUCTION_CHECKLIST.md`](docs/PRODUCTION_CHECKLIST.md) — it
covers the blockers (durable database, secrets, TLS, observability backend, distributed rate
limiting) alongside what is already hardened.

---

## Acceptance criteria (Definition of Done)

- [x] Search returns correct paged results for CRN, name (partial), DOB, nationality, and combinations.
- [x] Profile returns biographic data plus related ID cards and passports.
- [x] Angular form submits and shows paginated results; row click navigates to the profile.
- [x] Arabic and English names display; Arabic renders right-to-left.
- [x] OpenAPI documents all endpoints and they are testable in the API reference (Scalar).
- [x] Database seeded with 50+ persons (100), each with ≥1 ID card and ≥1 passport.
- [x] Code compiles, runs locally, and follows the layered architecture.

## License

[MIT](LICENSE) — proof-of-concept / educational. All data is synthetic.
