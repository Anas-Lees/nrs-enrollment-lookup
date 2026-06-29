# ADR 0004: Audit-safe Distributed Cache for Hot Profile Reads

- Status: Accepted
- Date: 2026-06-29
- Deciders: Solutions Architect, Backend Tech Lead
- Workstream: WS2/WS3 (hardening)

## Context
In an operator console the same applicant profile is opened repeatedly, so `GET /api/v1/persons/{crn}`
is the hot read. At the same time, this is a population registry: every lookup **must** be written to
an append-only audit trail (who looked up whom — see `AuditActionFilter` / `AuditLogger`). Any caching
we add must reduce database load **without** ever weakening that audit guarantee. A naive HTTP/output
cache would short-circuit the controller and skip the audit filter entirely — unacceptable here.

## Decision
Add **cache-aside** caching for profile reads via `IDistributedCache`:

- **Where:** a decorator, `CachedPersonLookupService`, wraps the application service. It caches only
  `GetByCrnAsync`. Search stays uncached (queries vary too widely; fresh result counts matter more).
- **Backend:** **Redis** when `ConnectionStrings:Redis` is set (shared across API instances), otherwise
  an in-memory `IDistributedCache` so local dev and tests need no external dependency. Same contract either way.
- **TTL:** 5 minutes (`AbsoluteExpirationRelativeToNow`). Profiles change rarely (issuance/registration events).
- **Misses are not cached** — a CRN that does not exist yet may be enrolled later.
- **Audit safety (the key constraint):** the decorator sits **below** the controller's audit filter, so
  the controller action — and therefore the audit record — runs on **every** request. A cache hit only
  skips the database round-trip, never the "who looked up whom" record. This is verified by a test that
  asserts two profile views (the second a cache hit) produce two audit rows.

**Messaging (RabbitMQ) is deliberately deferred.** A read-only lookup has no asynchronous work to hand
off, so introducing a broker now would be unused infrastructure. RabbitMQ is recorded here as the
**intended future seam for the enrollment-submission pipeline** (personalization, printing, quality
control, issuance), so its current absence reads as a decision rather than an oversight.

## Consequences

### Positive
- Fewer database round-trips on the hot profile path; a shared cache across API instances in the deployed stack.
- Audit integrity is preserved **by construction** — caching cannot hide an access.
- Zero external dependency for dev/test (in-memory fallback); Redis only where configured.

### Negative / trade-offs
- A cached profile can be up to 5 minutes stale (acceptable for this data; tune the TTL if needed).
- Redis is an added runtime dependency in the deployed stack (with graceful in-memory fallback otherwise).
- No active invalidation: staleness is bounded by the TTL only. A write path would need explicit eviction.

### Neutral / follow-ups
- Revisit the TTL and add explicit eviction if/when a profile write path is introduced.
- When the enrollment-submission feature lands, introduce RabbitMQ for its async downstream steps.

## Alternatives considered
- **HTTP output caching** — Rejected. It short-circuits the controller and would bypass the audit filter.
- **No cache** — Rejected. Repeated opens of the same profile hit the database unnecessarily.
- **Add RabbitMQ now** — Rejected. No async producer/consumer exists in a read-only lookup; it would be theatre.
