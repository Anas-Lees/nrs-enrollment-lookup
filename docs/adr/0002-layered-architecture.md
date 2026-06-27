# ADR 0002: Clean Layered Architecture

- Status: Accepted
- Date: 2026-06-28
- Deciders: Solutions Architect, Backend Tech Lead, Frontend Tech Lead
- Workstream: WS3 (with WS2 for Domain/Infrastructure)

## Context
The Applicant Lookup proof of concept is the seed of the wider enrollment module, which spans 185+ tables. The architecture must keep concerns cleanly separated so the POC can grow into the real module without rework, remain testable, and allow the data-access technology to be swapped without rippling through the application. We also want the layering itself to be enforceable rather than relying on convention alone.

## Decision
We will adopt a **four-layer architecture with dependencies pointing inward**:

- **Nrs.Api** — controllers and HTTP concerns. Depends on Application.
- **Nrs.Application** — services, DTOs, and interfaces. Depends on Domain.
- **Nrs.Domain** — entities and enums. Depends on nothing.
- **Nrs.Infrastructure** — EF Core, repositories, and seeding. Depends on Application + Domain, and is wired in at the API composition root.

Conventions:
- Thin controllers; business logic lives in services; data access lives in repositories.
- **DTOs over the wire** — EF entities are never exposed.
- Dependency injection used throughout.
- Entity-to-DTO mapping via AutoMapper.

An **Architecture.Tests** suite guards the layering (Domain references nothing; no EF in controllers).

## Consequences

### Positive
- Clear separation of concerns; the POC scales into the full enrollment module without rework.
- Highly testable: services and repositories can be tested in isolation behind interfaces.
- Swappable infrastructure — the data-access layer can change without touching Application or Domain.
- DTO boundary protects the API contract from internal entity changes.
- Automated architecture tests prevent layering erosion over time.

### Negative / trade-offs
- More projects, interfaces, and mapping code than a single-project approach — extra ceremony for a POC.
- AutoMapper adds an indirection that must be configured and kept correct.
- Developers must understand and respect the inward dependency rule.

### Neutral / follow-ups
- Keep the composition root (DI wiring) in the API project only.
- Maintain AutoMapper profiles alongside the DTOs they serve.
- Vertical-slice / CQRS remains a possible future evolution if the module's complexity warrants it.

## Alternatives considered
- **Single-project MVC** — Rejected. Does not scale to 185+ tables and mixes HTTP, logic, and data-access concerns.
- **Vertical-slice / CQRS** — Rejected for now. More ceremony than this POC needs, though noted as a possible future evolution as the module grows.
