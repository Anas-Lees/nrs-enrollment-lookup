# NRS Enrollment — Applicant Lookup

> Royal Oman Police · National Registration System (NRS) Modernisation
> **Enrollment track · Applicant Lookup feature** — a proof-of-concept built the way the production module will be built.

The Applicant Lookup screen is the entry point of the NRS Enrollment module. At any of the
58+ domestic sites or 75+ embassies, an operator searches for a person, opens their record,
and decides whether to start a new application or continue an existing one.

This repository delivers that feature as a clean, layered, bilingual (Arabic / English),
production-shaped slice: a documented REST API over EF Core and an Angular single-page app.

---

## Tech stack

| Layer        | Technology                                                              |
| ------------ | ---------------------------------------------------------------------- |
| Frontend     | Angular 21 (standalone components, reactive forms, router)             |
| Backend      | ASP.NET Core (.NET 10) — clean layered architecture                    |
| Data access  | Entity Framework Core (Oracle in prod, SQLite for local dev)           |
| API docs     | Swagger / OpenAPI (contract-first)                                     |
| Identity     | Keycloak (OIDC / JWT) — *stretch goal*                                 |
| Platform     | Docker, GitHub Actions CI/CD, OpenShift                               |

> **Local-dev note:** the production target is Oracle. To keep local setup light, the API runs
> against **SQLite** in `Development` and swaps in the Oracle provider via configuration — the
> de-risking path described in the delivery playbook ("learn on SQLite, switch provider late").

---

## Repository layout

```
nrs-enrollment-lookup/
├── backend/      ASP.NET Core solution (Api · Application · Domain · Infrastructure + tests)
├── frontend/     Angular 21 single-page app
├── deploy/       OpenShift / Kubernetes manifests + Keycloak realm
├── docs/         ADRs, the frozen OpenAPI contract, diagrams, onboarding
└── .github/      CI/CD workflows and repo policy
```

The full target tree (every file tagged with its workstream) lives in
[`docs/project-structure.md`](docs/project-structure.md).

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js 22+](https://nodejs.org/) and npm
- (optional) Docker — only needed for the full Oracle + Keycloak + Redis dev stack

### Run the backend

```bash
cd backend
dotnet restore
dotnet run --project src/Nrs.Api
# Swagger UI: https://localhost:7001/swagger
```

### Run the frontend

```bash
cd frontend
npm install
npm start
# App: http://localhost:4200  (proxies /api → backend)
```

---

## How this project is built

This feature is delivered as a set of small, reviewable steps — one per task — mirroring a
cross-functional product organisation working through seven parallel workstreams (WS1–WS7).
The plan, conventions, and quality gates are documented in the delivery playbook.

- **Architecture:** clean / layered, dependencies pointing inward (see [ADR 0002](docs/adr/0002-layered-architecture.md)).
- **Contract-first:** the [OpenAPI contract](docs/api/openapi.yaml) is the single source of truth.
- **Conventions:** trunk-based, Conventional Commits, PRs behind green checks.

## Acceptance criteria (Definition of Done)

- [ ] Search returns correct paged results for CRN, name (partial), DOB, nationality, and combinations.
- [ ] Profile returns biographic data plus related ID cards and passports.
- [ ] Angular form submits and shows paginated results; row click navigates to the profile.
- [ ] Arabic and English names display; Arabic renders right-to-left.
- [ ] Swagger documents all endpoints and they are testable there.
- [ ] Database seeded with 50+ persons, each with ≥1 ID card and ≥1 passport.
- [ ] Code compiles, runs locally, and follows the layered architecture.

## License

[MIT](LICENSE) — proof-of-concept / educational. All data is synthetic.
