# ADR 0006: Camunda 8 for the Enrollment Review Workflow

- Status: Accepted
- Date: 2026-07-05
- Deciders: Solutions Architect, Backend Tech Lead
- Workstream: WS3

## Context
An enrollment moves through a small lifecycle: `SUBMITTED → UNDER_REVIEW → APPROVED | REJECTED`.
Until now that lifecycle was implied by code — a RabbitMQ consumer advanced `SUBMITTED` to
`UNDER_REVIEW`, and there was no first-class "approve / reject" step at all. That works, but the
process is invisible: there is no diagram, no audit of where an application is, and no single place
that owns the transitions. As the review grows (extra checks, timeouts, escalations), encoding it in
scattered event handlers gets harder to reason about and change.

We wanted the review to be an **explicit, observable process** — something a non-developer can look
at and understand — without rewriting the rest of the app around it.

## Decision
We will orchestrate the enrollment review with **Camunda 8 (self-hosted)**, modelled as a BPMN
process, and integrate over its **REST API (`/v2`)**.

- **The process** (`enrollment-review.bpmn`) is: start → *Mark under review* (service task) → wait for
  the *decision* message → gateway → *Approve* or *Reject* service task → end. It is versioned with
  the code and deployed by the API on startup.
- **The API is an external job worker.** A background service long-polls Camunda for the three
  service-task jobs (`mark-under-review`, `apply-approved`, `apply-rejected`) and applies the matching
  status to the enrollment in Oracle. Camunda owns the *flow*; the app owns the *side effects*.
- **Creating** an enrollment starts a process instance; **deciding** (`POST /enrollments/{id}/decision`)
  correlates the `enrollment-decision` message the instance is waiting on, keyed by the enrollment id.
- **REST over gRPC.** We use the `/v2` REST API, not the Zeebe gRPC client — gRPC was deprecated in
  8.8 and the REST API is the supported surface. No Zeebe client library is taken as a dependency; the
  integration is a thin typed `HttpClient`.
- **It is optional and feature-flagged.** Camunda is used only when `Camunda:RestAddress` is
  configured. With it unset, a direct-to-database fallback applies decisions and the app runs exactly
  as before — so local dev, unit and integration tests need no engine.

## Consequences

### Positive
- The review is a **picture, not a paragraph of code**: the BPMN is the single source of truth, and
  Camunda Operate shows exactly where every application is.
- Adding steps (a second approver, a timeout/escalation, a parallel background check) is a model
  change, not a rewrite of event plumbing.
- The engine is decoupled: it drives the flow but never touches the database — the worker does, using
  the same EF Core context and status rules as the rest of the app.
- No lock-in at the wire level — a plain `HttpClient` against documented REST, feature-flagged off by
  default.

### Negative / trade-offs
- The full local stack is heavier: Camunda needs **Elasticsearch**, adding ~2 containers and a couple
  of GB of RAM. (The compose file pins small JVM heaps to keep it manageable.)
- One more moving part to run and understand; a decision is applied **asynchronously** (the worker
  completes the job a moment after the message is correlated), which the decision endpoint hides
  behind a short bounded wait.
- Self-hosting Camunda is an operational commitment (upgrades, Elasticsearch retention) if this ever
  went beyond a demo.

### Neutral / follow-ups
- Keep the BPMN and the worker's job types in lockstep — a renamed task type silently strands jobs.
- If the review stays this simple long-term, revisit whether the engine earns its operational cost.

## Alternatives considered
- **Keep the code-only lifecycle (RabbitMQ consumer + a decision endpoint writing straight to the
  DB)** — Retained as the *fallback path*, but rejected as the primary design because the process
  stays invisible and has no room to grow.
- **Zeebe gRPC client** — Rejected. Deprecated from 8.8; the REST API is the forward-looking surface
  and needs no generated client.
- **A lighter workflow library (e.g. Elsa, a state-machine library)** — Reasonable, but Camunda's BPMN
  + Operate gives the "anyone can read the process" property that motivated this decision.

This ADR builds on [ADR 0002](0002-layered-architecture.md) (the enrollment feature is a vertical
slice) and does not supersede any existing ADR.
