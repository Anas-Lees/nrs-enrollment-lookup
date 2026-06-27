# ADR 0003: EF Core over Oracle, SQLite for Local Development

- Status: Accepted
- Date: 2026-06-28
- Deciders: Solutions Architect, Backend Tech Lead, Frontend Tech Lead
- Workstream: WS2

## Context
The enrollment system runs on Oracle in production, but standing up and learning Oracle is the top project risk and would slow every developer who needs a local database. We want developers to be productive immediately, to iterate quickly, and to defer the Oracle-specific setup until the schema and access patterns have settled. The brief favours EF Core, and Arabic name data requires proper Unicode handling. Search and lookup performance must be considered from the outset.

## Decision
We will use **EF Core with the repository pattern**, selecting the database provider by configuration/environment:

- **Production provider:** Oracle (`Oracle.EntityFrameworkCore`).
- **Local/dev provider:** SQLite, so developers need no Oracle installation.

The Oracle provider is swapped in late. Specifics:
- Arabic name columns use **NVARCHAR2** (Unicode) on Oracle.
- Migrations are managed via `dotnet ef`.
- Seed **50–100 persons** with Bogus, each having **>= 1 ID card** and **>= 1 passport**.
- Indexes on **CRN** and the search columns.
- **Offset paging** now, with the design kept **keyset-ready**.

## Consequences

### Positive
- Strong developer productivity: no local Oracle install; learn and iterate quickly on SQLite.
- De-risks the top project risk by deferring Oracle setup and the associated learning curve.
- Repository pattern keeps data access behind interfaces and isolates provider-specific concerns.
- Indexing and keyset-ready paging position the lookup for good search performance at scale.

### Negative / trade-offs
- Behavioural differences between SQLite and Oracle (collation, NVARCHAR2, case-insensitivity, sequences) can mask issues that only surface on the real provider.
- Maintaining two providers adds configuration surface and discipline.
- Switching the provider late concentrates Oracle-specific validation near the end.

### Neutral / follow-ups
- Run **integration tests against the real (Oracle) provider** to catch provider-specific differences.
- Keep provider-specific configuration isolated so each provider's quirks are contained.
- Revisit offset vs keyset paging once data volumes and access patterns are known.

## Alternatives considered
- **Dapper / raw SQL** — Rejected. Less productive for this team, and the brief prefers EF Core.
- **Oracle-only local via Docker** — Rejected. Heavy local setup, and Docker availability is not assumed across developer machines.
