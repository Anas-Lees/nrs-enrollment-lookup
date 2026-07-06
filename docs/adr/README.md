# Architecture Decision Records

This directory records the significant architecture decisions for the NRS Enrollment **Applicant Lookup** project (Royal Oman Police National Registration System modernisation).

Each ADR captures the context, the decision, and its consequences so that future contributors understand not just *what* was decided but *why*.

## Index

| ADR | Title | Status | Workstream |
| --- | --- | --- | --- |
| [0001](0001-rest-openapi.md) | REST API with OpenAPI, Contract-First | Accepted | WS3 + Architecture |
| [0002](0002-layered-architecture.md) | Clean Layered Architecture | Accepted | WS3 (with WS2) |
| [0003](0003-ef-core-oracle.md) | EF Core over Oracle, SQLite for Local Development | Superseded by [0005](0005-oracle-only.md) | WS2 |
| [0004](0004-audit-safe-distributed-cache.md) | Audit-safe Distributed Cache for Hot Profile Reads | Accepted | WS2/WS3 |
| [0005](0005-oracle-only.md) | Oracle for Every Environment (SQLite Removed) | Accepted | WS2 |
| [0006](0006-camunda-workflow.md) | Camunda 8 for the Enrollment Review Workflow | Accepted (decision mechanism superseded by [0007](0007-human-in-the-loop-review.md)) | WS3 |
| [0007](0007-human-in-the-loop-review.md) | Human-in-the-Loop Review — User Tasks, Screening, SLA Escalation, Roles | Accepted | WS3 |

## ADR process

We follow a lightweight [MADR](https://adr.github.io/madr/)-style format. ADRs are numbered sequentially (`NNNN-short-title.md`) and are immutable once accepted — a decision is changed by writing a new ADR that supersedes the old one, not by editing history.

### Status lifecycle

- **Proposed** — the decision is drafted and under discussion; not yet binding.
- **Accepted** — the decision is agreed and in force. This is the default state for the ADRs in this index.
- **Superseded** — the decision has been replaced by a later ADR. The superseded record stays in place for the historical trail and links forward to its replacement.

```
Proposed -> Accepted -> Superseded
```

### Writing a new ADR

1. Copy the structure of an existing ADR (Context, Decision, Consequences, Alternatives considered).
2. Assign the next sequential number.
3. Set the status to **Proposed**, list the deciders, and circulate for review.
4. On agreement, change the status to **Accepted** and add it to the index table above.
5. If a future decision overrides it, mark it **Superseded** with a link to the replacing ADR.
