# Target project structure

This is the full target tree for the NRS Applicant Lookup solution. Each entry is tagged with
the **workstream** that owns it, so any contributor can see where their work fits.

> This document is the planning blueprint. Files appear in the repository as their owning task
> lands — see the delivery backlog. Nothing here is load-bearing at runtime; it is the map.

## Workstreams

| Tag   | Workstream            | Owner          |
| ----- | --------------------- | -------------- |
| `WS1` | Platform & CI/CD      | DevOps         |
| `WS2` | Data & Persistence    | DBA + Backend  |
| `WS3` | Backend API           | Backend squad  |
| `WS4` | Frontend App          | Frontend squad |
| `WS5` | Design & UX           | Designer       |
| `WS6` | Quality & Test        | QA / SDET      |
| `WS7` | Security & IAM        | Security       |

## Tree

```
nrs-enrollment-lookup/            # Git repository root — the whole solution lives here
├── README.md                     # Overview, prerequisites, how to run backend + frontend locally
├── .gitignore                    # Ignore build output, node_modules, bin/obj, .env
├── .editorconfig          [WS1]  # Shared formatting rules (C# + TS) — enforced in CI
├── .gitattributes                # Line-ending normalisation
├── LICENSE                       # Licence / usage terms
├── CODEOWNERS             [WS1]  # Auto-requests the right reviewers per folder
├── docker-compose.yml     [WS1]  # Local Oracle XE + Keycloak + Redis for development
├── .github/               [WS1]  # CI/CD automation & repo policy
│   ├── workflows/         [WS1]
│   │   ├── ci.yml                # Build → test → lint → security scan on every PR
│   │   └── cd.yml                # Deploy to Dev on merge; promote to Test/Stage/Prod
│   └── pull_request_template.md  # PR checklist: tests, docs, acceptance criteria
├── docs/                         # Living documentation
│   ├── adr/                      # Architecture Decision Records (one file per decision)
│   │   ├── 0001-rest-openapi.md
│   │   ├── 0002-layered-architecture.md
│   │   └── 0003-ef-core-oracle.md
│   ├── api/
│   │   └── openapi.yaml          # The frozen API contract — single source of truth
│   ├── diagrams/                 # Architecture, ERD, sequence, CI/CD exports
│   └── onboarding.md             # New-developer setup guide
├── backend/                      # ASP.NET Core solution — clean layered architecture
│   ├── Nrs.ApplicantLookup.sln
│   ├── Directory.Build.props     # Shared build settings (nullable on, analyzers, lang version)
│   ├── src/
│   │   ├── Nrs.Api/        [WS3]  # API layer — thin controllers, HTTP concerns only
│   │   ├── Nrs.Application/[WS3]  # Application layer — business logic, DTOs, interfaces
│   │   ├── Nrs.Domain/     [WS2]  # Domain layer — pure entities & rules, no dependencies
│   │   └── Nrs.Infrastructure/ [WS2]  # EF Core, repositories, seeding
│   ├── tests/             [WS6]  # Unit, integration, contract, architecture tests
│   └── Dockerfile         [WS1]
├── frontend/              [WS4]  # Angular 21 single-page app (standalone components)
│   ├── package.json
│   ├── angular.json
│   ├── proxy.conf.json           # Dev proxy → backend API (avoids CORS locally)
│   ├── src/
│   │   ├── app/
│   │   │   ├── core/      [WS4]  # models, services, interceptors [WS7], guards [WS7]
│   │   │   ├── features/  [WS4]  # search/ and profile/ pages
│   │   │   └── shared/    [WS4]  # pagination, document-table, status-badge, pipes
│   │   ├── environments/
│   │   └── assets/i18n/   [WS5]  # en.json / ar.json (RTL)
│   └── e2e/               [WS6]  # search-to-profile.spec.ts
└── deploy/                [WS1]  # Infrastructure & deployment manifests
    ├── openshift/         [WS1]  # api/spa deployments, service, route, configmap, secret
    └── keycloak/          [WS7]  # realm-export.json (stretch)
```

For the fully annotated tree (every leaf file and its purpose), see the source planning
artifact `NRS_Project_Structure.txt`.
