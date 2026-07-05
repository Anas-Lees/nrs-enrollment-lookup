# Production Readiness Checklist

The application code is built secure-by-default; the **demo deployment** (Docker Compose /
the OpenShift sample) intentionally opts into convenient-but-unsafe values. Before any real
deployment, work through the items below. Status reflects a 2026‑07‑05 readiness audit.

## 🔴 Blockers — must fix before production

- [ ] **Real, durable database.** The app always runs on Oracle, but the samples point at a
  throwaway demo Oracle (single container / pod‑local) that is fine for a POC and unfit for
  production — data and the audit trail are not durable and would not survive a restart.
  Provision a managed/enterprise Oracle, inject its connection string from a secret, and set up
  **backups + PITR + a tested restore**.
- [ ] **Don't seed or auto-migrate on boot in prod.** Set `Database__SeedOnStartup=false` and run
  migrations as a one‑shot Job/init‑container (not `MigrateAsync()` racing across replicas with
  DDL rights). The OpenShift `configmap.yaml` currently sets `Database__SeedOnStartup: "true"`
  (demo only).
- [ ] **Observability backend.** OpenTelemetry is instrumented but exports nowhere unless
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set. Point it at a collector, expose metrics (Prometheus),
  ship stdout logs centrally, and define golden‑signal **alerts** (availability, latency, 5xx,
  429, audit‑write failures).
- [ ] **Durable, tamper-evident audit.** Once on Oracle: grant the app account `INSERT`/`SELECT`
  only on `AUDIT_ENTRY` (or use Oracle immutable tables), ship audit events off‑box, and define
  retention/archival/partitioning.
- [ ] **TLS on the API leg.** Add `UseHttpsRedirection` + `UseHsts` (with forwarded‑headers), and
  use re‑encrypt/passthrough TLS or a service mesh so SPA→API isn't cleartext. Keep
  `Auth__RequireHttpsMetadata=true` and reach Keycloak over TLS (the demo sets it `false`).
- [ ] **Secrets out of source.** Move every secret to a secret store (OpenShift Secrets / Vault /
  Sealed Secrets) injected via `secretKeyRef`. The demo `docker-compose.yml` inlines Oracle/app
  passwords and Keycloak `admin/admin`; `deploy/openshift/secret.yaml` is a `CHANGE_ME` template
  not yet wired into the deployment.
- [ ] **Distributed rate limiter.** The lookup limiter is in‑memory/per‑pod, so the effective
  limit is `N × PermitLimit`. Back it with Redis (shared sliding window) for multi‑replica.
- [ ] **No anonymous API docs in prod.** Keep `OpenApi:Enabled=false` (the safe default) in any
  internet‑reachable environment.

## 🟡 Should fix

- [ ] Deploy by **image digest** (not `:latest`) so rollback/rollout is deterministic.
- [ ] Make **Trivy/CodeQL gate** the build (currently `continue-on-error`, scanned after push);
  add a SARIF sink and Dockerfile/IaC scanning over `deploy/**`.
- [ ] Add a CD **deploy + smoke-test + rollback** job on a verified digest.
- [ ] Add an **HPA** and a **PodDisruptionBudget** (`minAvailable: 1`); replicas are hard‑pinned at 2.
- [ ] Add the **HTTP security headers / CSP at the SPA nginx** (the API already sets
  `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Strict-Transport-Security`).
- [ ] Pin **`AllowedHosts`** (currently `*`) and set **`Cors:AllowedOrigins`** to the real SPA origin.
- [ ] Store `DateOnly` as native Oracle `DATE` (currently `NVARCHAR2(10)`) before the schema locks.
- [ ] Enforce a **CI coverage threshold** (collected but not gated).

## ✅ Already in place

- Secure‑by‑default config: `RequireHttpsMetadata` defaults true; OpenAPI docs default off
  outside Development; seeding defaults off under Production — asserted by `ProductionModeTests`.
- Auth: Authorization Code + PKCE (S256), public SPA / bearer‑only API client; strict JWT
  validation (issuer/audience/lifetime/signing‑key, 60s skew); fallback policy requires an
  authenticated user **with** the `operator` role on every non‑anonymous endpoint.
- Keycloak realm hardened: brute‑force lockout, `registrationAllowed=false`, **direct‑access‑grants
  disabled** on the public client, redirect/web‑origins pinned (no `https://*` / `+`), and a
  **password policy** (length 12 + complexity).
- Container hardening (OpenShift): non‑root, read‑only FS, drop ALL caps, seccomp RuntimeDefault,
  dedicated SAs, default‑deny NetworkPolicy, no public Route for the API.
- Audit‑before‑disclosure correctness; cache decorator sits below the audit filter and is
  **best‑effort** (a Redis outage falls through to the DB, never a 500).
- Input hardening: CRN route constraint (malformed → 404, not 500) + defensive audit‑column
  clamping; RFC‑7807 errors that leak no internals; rate limiting + boundary validation.
- CI breadth: build/test/coverage, blocking NuGet High/Critical vuln gate, CodeQL, Trivy, SBOM,
  Dependabot, least‑privilege workflow permissions.

## Production config flags (assert these)

| Setting | Demo | Production |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (compose) | `Production` |
| `OpenApi:Enabled` | `true` (compose) | **`false`** |
| `Auth:RequireHttpsMetadata` | `false` | **`true`** |
| `Database:SeedOnStartup` | `true` | **`false`** |
| `Database:InitializeOnStartup` | default | **`false`** (migrate via Job) |
| `AllowedHosts` | `*` | **real host(s)** |
| `Cors:AllowedOrigins` | `http://localhost:4200` | **real SPA origin(s)** |
