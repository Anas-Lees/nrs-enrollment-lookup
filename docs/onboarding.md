# Developer onboarding

Welcome to the NRS Enrollment **Applicant Lookup** team. This guide gets you from a fresh clone
to a running app, and points you at the conventions you need on day one.

## 1. Prerequisites

| Tool | Version | Notes |
| ---- | ------- | ----- |
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | 22+ | `node --version` |
| Git | 2.40+ | |
| Docker | Required | For the local Oracle (and the full Keycloak + Redis dev stack) |

**All environments use Oracle**, including local development. Start a local Oracle with
`docker compose up -d oracle`; it listens on `localhost:1521/XEPDB1` (app user `nrs_app`). There is
no provider toggle — the app is always Oracle (see [ADR 0005](adr/0005-oracle-only.md)).

## 2. Clone

```bash
git clone https://github.com/Anas-Lees/nrs-enrollment-lookup.git
cd nrs-enrollment-lookup
```

## 3. Run the backend

```bash
docker compose up -d oracle          # local Oracle on localhost:1521/XEPDB1
cd backend
dotnet restore
dotnet run --project src/Nrs.Api
```

- API reference (Scalar): `https://localhost:7001/scalar`
- On startup the API applies EF Core migrations and seeds 50–100 persons (outside Production).

## 4. Run the frontend

```bash
cd frontend
npm install
npm start        # http://localhost:4200
```

The dev server proxies `/api` to the backend (see `proxy.conf.json`), so there are no CORS
issues locally.

## 5. Run the tests

```bash
# Backend
cd backend && dotnet test

# Frontend unit tests
cd frontend && npm test

# End-to-end
cd frontend && npm run e2e
```

## 6. The contract is the source of truth

Before changing any request/response shape, read and update
[`docs/api/openapi.yaml`](api/openapi.yaml). The backend implements it, the frontend mocks it,
and contract tests enforce it. Changes go through review (see [ADR 0001](adr/0001-rest-openapi.md)).

## 7. Conventions (the short version)

- **Branches:** `feature|fix|chore/WSx-short-slug` (e.g. `feature/WS3-search-pagination`).
- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/) — `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`.
- **PRs:** fill the template, link the issue, keep them small; merge behind green checks.
- **.NET:** nullable on, async all the way (`Async` suffix), one responsibility per class, no EF Core outside the data layer.
- **Angular:** standalone components, OnPush change detection, typed reactive forms, smart/dumb split, ESLint + Prettier.

## 8. Where things live

| You want to… | Look in |
| ------------ | ------- |
| Understand the architecture | [`docs/adr/`](adr/) |
| See the API contract | [`docs/api/openapi.yaml`](api/openapi.yaml) |
| See the full repo map | [`docs/project-structure.md`](project-structure.md) |
| Work on the API | `backend/src/Nrs.Api` (and Application/Domain/Infrastructure) |
| Work on the UI | `frontend/src/app` |
| Work on deployment | `deploy/` and `.github/workflows/` |

Stuck? Open an issue using the templates, or ask in the team channel.
