# ADR 0005: Oracle for Every Environment (SQLite Removed)

- Status: Accepted
- Date: 2026-07-05
- Deciders: Solutions Architect, Backend Tech Lead
- Workstream: WS2

## Context
[ADR 0003](0003-ef-core-oracle.md) kept SQLite for local development and selected the database
provider by configuration (`DatabaseProvider`), deferring Oracle to Test/Stage/Prod. That bought
early productivity, but it also meant every developer ran against a provider that behaves
differently from production: collation, case-insensitivity, `NVARCHAR2`, sequences and identity
keys all diverged, so a class of bugs only surfaced late — on the real provider. Maintaining two
providers (and two migration sets) added configuration surface and discipline for little ongoing
benefit now that the Oracle provider is implemented and Docker is available across the team.

## Decision
We will use **Oracle in every environment, including local development**. Specifically:

- **SQLite is removed entirely.** There is no longer a lightweight in-process provider.
- **A single Oracle migration set** lives in `Nrs.Infrastructure`. The separate
  `Nrs.Infrastructure.Migrations.Oracle` project is **deleted** and its migrations are consolidated
  back into `Nrs.Infrastructure`.
- **The `DatabaseProvider` config switch is removed.** The application always uses Oracle; there is
  no provider toggle to set.
- **Local development requires a local Oracle.** Start it with `docker compose up -d oracle`; it
  listens on `localhost:1521/XEPDB1` (app user `nrs_app`). The API applies migrations on startup and
  seeds data outside Production.
- **Integration tests use Testcontainers Oracle** — they spin up a disposable Oracle container and
  **auto-skip when Docker is unavailable**, so tests exercise the real provider by default while
  staying runnable on machines without Docker.

## Consequences

### Positive
- Dev, test and prod exercise the **same** provider — collation, `NVARCHAR2`, case-insensitivity,
  sequences and identity keys behave identically everywhere, so provider-specific bugs surface early.
- One migration set and no provider branch: less configuration surface, less to keep in sync.
- Integration tests validate against real Oracle by construction, not as an occasional extra step.

### Negative / trade-offs
- Local development now requires Docker and a running Oracle container — a heavier prerequisite and a
  slower cold start than the old in-process SQLite.
- Oracle XE consumes more local resources than SQLite did.
- Running the full integration suite requires Docker (it auto-skips otherwise, at the cost of coverage).

### Neutral / follow-ups
- Keep the `docker compose up -d oracle` step documented in the README and onboarding guide.
- Store `DateOnly` as native Oracle `DATE` (currently `NVARCHAR2(10)`) before the schema locks.

## Alternatives considered
- **Keep SQLite for dev (ADR 0003)** — Rejected. The dev/prod provider gap masks real issues and the
  two-provider maintenance cost is no longer justified now that Oracle is implemented.
- **In-memory EF provider for tests** — Rejected. It is not a relational database and would reintroduce
  exactly the behavioural gap this decision removes.

This ADR **supersedes** [ADR 0003](0003-ef-core-oracle.md).
