# ADR 0007: Human-in-the-Loop Review — User Tasks, Screening, SLA Escalation, Roles

- Status: Accepted
- Date: 2026-07-06
- Deciders: Solutions Architect, Backend Tech Lead
- Workstream: WS3

## Context
[ADR 0006](0006-camunda-workflow.md) put the enrollment review on Camunda 8, but the process was
still a skeleton: every application went to review, "review" was a message the API correlated,
any operator could decide, decisions carried no reasoning, and nothing chased a review that sat
for days. That is not how a registration office works. In real life a clean renewal should not
wait behind a fraud check; deciding is a *reviewer's* job, not the counter operator's; rejections
must carry a reason the applicant can be told; overdue reviews get escalated to a supervisor; and
staff find their work in a task list, not by refreshing a table.

## Decision
Grow `enrollment-review` into a human-in-the-loop process, and use a Camunda-native feature for
each real-world need:

- **Automated screening (service task).** On submission a `screen-application` worker checks the
  registry: does the CRN exist and match? is the record active? is there a duplicate pending
  application? is the applicant a minor needing guardian documents? Flags are persisted on the
  enrollment so the reviewer sees *why* it reached them.
- **Straight-through processing (gateway).** A clean renewal of a known record is auto-approved
  (`decidedBy = "auto-screening"`) and never waits for a human. Everything else routes to review.
- **The review is a real user task** (`zeebe:userTask`, candidate group `reviewer`) — not a
  message. It appears in the app's **Review Tasks** screen (claim → approve / reject-with-reason)
  and, identically, in Camunda Tasklist. The API completes the task via `/v2/user-tasks`.
- **SLA escalation (boundary timer).** A non-interrupting timer (`escalationAfter`, from
  configuration: 48 h production-shaped, 2 min in the demo compose) notifies supervisors when a
  review is overdue and stamps `EscalatedAtUtc`; the review itself stays open.
- **Notifications (service tasks + worker).** The process notifies reviewers when work queues,
  the submitting operator when a decision lands, and supervisors on escalation — written to a
  NOTIFICATION table and surfaced by a bell in the SPA (30 s polling; deliberately boring).
- **Roles.** Keycloak realm roles `operator` / `reviewer` / `supervisor` (demo users operator1,
  reviewer1, supervisor1). Deciding and the review queue require the reviewer role ("CanReview"
  policy); the SPA hides what a role cannot do. With auth off (local POC) every role is granted.
- **Decision audit.** `DecidedBy`, `DecidedAtUtc`, `DecisionNotes` are persisted; a reason is
  mandatory for rejections.

## Consequences

### Positive
- The BPMN now *is* the office's standard operating procedure — screening, routing, human
  decision, SLA — readable by a non-developer in Operate.
- Straight-through processing: clean renewals settle in seconds with no reviewer time spent.
- Accountability: every decision says who, when and why; overdue work escalates by itself.
- The same user task drives both the in-app queue and Camunda Tasklist — no parallel truth.

### Negative / trade-offs
- User-task search is Elasticsearch-backed and eventually consistent (a just-created task can
  lag a second or two); the decision path retries briefly and falls back to a direct write for
  orphaned enrollments.
- More moving parts in the process (5 worker types); the BPMN and the workers' job types /
  variables must stay in lockstep.
- Screening rules live in code (`EnrollmentScreening`), not DMN — fine at this size; a decision
  table would be the next step if the rules grow.

## Alternatives considered
- **Keep message correlation for decisions** — Rejected: user tasks model human work first-class
  (assignment, candidate groups, Tasklist) where a message models an external event.
- **DMN decision table for screening** — Deferred: five rules don't justify another artifact yet.
- **Push notifications (SSE/websockets)** — Rejected for now: minutes-scale events; 30 s polling
  is simpler and sufficient.

This ADR **extends** [ADR 0006](0006-camunda-workflow.md) (which stays accepted for the
engine choice and worker pattern) and supersedes its message-correlation decision mechanism.
