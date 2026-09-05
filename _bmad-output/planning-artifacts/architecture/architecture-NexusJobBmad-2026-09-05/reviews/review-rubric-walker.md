---
review: rubric-walker
target: ../ARCHITECTURE-SPINE.md
reviewer-role: rubric walker (fixed good-spine checklist)
date: 2026-09-05
verdict: SHIP WITH FIXES — spine is strong on its stated purpose (enforced module boundaries); no critical defects, but two HIGH findings (stale PostgreSQL version claim; the UX apply-gate compound flow has no architectural home) and several MEDIUM gaps in cross-cutting contracts and the operational envelope should be closed before epics/stories derive from it.
---

# Rubric Walk — ARCHITECTURE-SPINE.md (NexusJob v1)

Scope reviewed: `_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md` in full.
Context read for judgment: PRD, brief + addendum, EXPERIENCE.md, `.memlog.md`, and web verification of named tech versions (2026-09-05).

---

## Checklist

### 1. Fixes the real divergence points for the level below (epics/stories) and misses none

**CONCERN.**

Covered well — the divergence points that matter for a boundary-practice modular monolith are each pinned by an AD:

| Divergence point | Fixed by |
| --- | --- |
| Module list + data ownership | AD-4 |
| Boundary rule + its CI enforcement | AD-1, AD-2 |
| Internal structure (VSA vs layered) | AD-3 |
| Schema ownership / no cross-schema FK | AD-5, AD-7 |
| Inter-module comms style | AD-6 |
| Transaction boundary | AD-8 |
| FR-7 ownership check placement | AD-9 |
| Composition root | AD-10 |
| Email-uniqueness model | AD-11 |
| Deployment topology / same-origin | AD-12 |
| Auth mechanism | AD-13 |
| No mediator library | AD-14 |
| HTTP contract shape + generated client | AD-15 |
| Frontend FSD layering | AD-16 |
| Frontend↔backend slice mapping | AD-17 |
| Anonymous vs authenticated endpoints | AD-18 |

Misses / half-decisions that two units could still diverge on:

- **(HIGH) The UX apply-gate compound flow has no home.** EXPERIENCE.md Flow 2 / `apply-gate-modal`: a signed-out visitor's single "Apply" click creates an Identity account *and then auto-submits an Application against the posting already on screen*, with a defined partial-failure path (account created, application submit fails, modal closes, retry without re-entering credentials). The spine never addresses where this orchestration lives or what its contract is — a combined Host-level endpoint? a client-orchestrated register-then-apply pair of `/api` calls? — nor the partial-failure semantics. AD-8 ("one user action mutates exactly one module") arguably *forbids* a combined server endpoint, which pushes orchestration to the client, but the spine never says so. Two epics can build this incompatibly.
- **(MEDIUM) Pagination contract is only half-specified.** AD-15 mandates "a small typed page object (`items` + paging fields)" for search results and applicants, and EXPERIENCE.md mandates pagination (not infinite scroll) on both surfaces — but no query-param names (`page`/`pageSize` vs `offset`/`limit`), no default/max page size, no 0- vs 1-based indexing. Feature-altitude divergence point, left open.
- **(MEDIUM) Validation-error shape not pinned.** AD-15 fixes errors to RFC 9457 ProblemDetails but does not specify the field-level validation member the frontend needs. EXPERIENCE.md requires inline per-field errors "programmatically associated with their field." Builders can diverge (ASP.NET `ValidationProblemDetails.errors` dictionary vs a custom `extensions` map).
- **(MEDIUM) "List my postings" (Company) has no slice and no map row.** EXPERIENCE.md IA ("My Postings + Applicants" surface) and Flow 1 step 7 require a Company to list its own postings. The source tree's JobPostings slices are only `{CreatePosting, SearchPostings, GetPosting}`; the Capability→Architecture Map has no row for it. Implied by FR-7's drill-in but never homed.
- **(LOW) Post-registration auto-apply aside:** a health endpoint is named in `.memlog.md` but absent from the spine (see item 6).

### 2. Every AD's Rule is enforceable and actually prevents its stated divergence

**CONCERN.**

- AD-1 / AD-2 — strong. Concrete, named tooling (ArchUnitNET three rules + ESLint FSD rule), explicitly build-breaking, declared a primary deliverable. This is the spine's best work and directly discharges SM-2.
- AD-3, AD-6, AD-10, AD-12, AD-16, AD-18 — enforceable (structure, DI wiring, `AllowAnonymous` attributes, lint) and each prevents its divergence.
- **(MEDIUM) AD-5 over-claims its enforcement.** "Direct SQL or EF access across a schema boundary is forbidden (and caught by AD-2's rule 1 via the DbContext's assembly)." ArchUnitNET rule 1 catches *assembly* dependencies; a module issuing raw ADO.NET / `FromSqlRaw` against another schema through its *own* DbContext introduces no cross-assembly type reference and is not caught. The rule partly rests on the developer discipline it claims to have automated. Either add a real check (e.g. a schema-name lint / migration-history assertion) or state honestly that this clause is convention-enforced.
- AD-7 (no cross-schema FK), AD-8 (single `SaveChanges`) — convention-enforced via migration/PR review, not automated. Acceptable at this altitude but not "enforceable" in the AD-2 sense; fine as long as that's understood.
- AD-14 ("no mediator library") — enforceable via a banned-package check or an arch test, but neither is specified.
- AD-17 — has a real checkpoint ("after the first vertical slice ships"); enforceable as process.

### 3. Nothing under Deferred could let two units diverge

**CONCERN.** The Deferred list is mostly clean — async messaging (also barred by AD-6), full layering (new AD required), posting lifecycle (JobPostings solely owns mutation), BFF/YARP/CORS (removed by AD-12), micro-frontends, staging/prod split, rate-limiting/lockout/reset, caching/read-models/FTS, multi-tenancy — each is safely deferrable and contained.

Two items are softer:

- **(MEDIUM) AD-13 leaves Data Protection key persistence as "persisted (volume or DB)".** That unresolved either/or is a divergence point: one story implements filesystem-volume key persistence, another a DB key store. Compounded by the undecided hosting platform (below). Pick one, or make the choice explicitly a function of the hosting decision.
- **(LOW) Hosting platform** is deferred *and* flagged undecided in the Deployment section. Deferring the provider is reasonable, but it touches the migration-run step, key persistence, and secrets injection. "Decide before first deploy" is acceptable given SM-1, but note the mild tension with SM-1's "deployed and usable."
- NSwag exact-version pin deferred to the client-generation story — contained, fine.

### 4. Named tech is verified-current (versions dated 2026-09-05)

**FAIL.**

- **(HIGH) PostgreSQL 17 is not current as of 2026-09-05.** PostgreSQL 18.0 was released 2025-09-25 — roughly a year before the spine's date — and 18.6 is the current point release (Aug 2026). The Stack table header claims "verified current on the web 2026-09-05," and PostgreSQL 17 fails that claim. Either this is a stale verification, or PG 17 is a deliberate conservative choice that the spine should state and justify. Note PG 18 also ships a native `uuidv7()` function and virtual generated columns, which intersect the spine's "UUID v7 generated in application code" convention — worth a sentence either way.
- Verified current and correct: **.NET 10 / C# 14** (GA 2025-11-11, LTS to 2028-11), **ASP.NET Core 10**, **EF Core 10 + Npgsql 10**, **React 19.2.x** (19.2.8 by Jul 2026), **Vite 8.x** (8.0 Mar 2026, Rolldown), **TanStack Query v5**, **React Router v7**, **TypeScript 5.x**.
- Verified: **MediatR went commercial** (RPL-1.5 dual license, v13, Jul 2025) — AD-14's rationale is accurate. **NetArchTest stale since 2023 / ArchUnitNET actively maintained** — accurate.
- **(LOW) Imprecise rows.** "ArchUnitNET — current", "Testcontainers for .NET — current", and NSwag (unversioned) are not given the dated version the table header promises for every other row. Pin or date them.

### 5. Covers the PRD's capabilities (FR-1..FR-7, NFRs, SM-1/SM-2, SM-C1)

**PASS (with one gap).**

- FR-1..FR-7 each appear in the Capability→Architecture Map with a home module/slice and governing ADs; FR-7's "not found, never confirm existence" is carried through from EXPERIENCE.md into AD-9. FR-5's company-name read and FR-7's applicant name/email read are routed through Contracts (AD-6).
- NFR-module-boundary → AD-1/AD-2 + `NexusJob.ArchitectureTests` (discharges SM-2, strongly). NFR-credential-security → AD-13 (PBKDF2-HMAC-SHA256, never logged). NFR-platform → AD-12.
- SM-1 (full loop deployed) → Deployment section + CI "one full-core-loop e2e test". SM-C1 (anti-ceremony) → explicitly cited in AD-3, AD-14, and three Deferred rationales; genuinely load-bearing in the design, not decorative.
- **(LOW) Gap:** the Job Seeker "My Applications" list (EXPERIENCE.md IA + Flow 2 step 8) has a source-tree slice (`ListMyApplications`) but no numbered FR, no Capability→Architecture Map row. The spine flags PRD OQ-4 and OQ-3 for upstream correction but does not flag this PRD gap the same way.

### 6. Every structural dimension the altitude owns is decided / deferred / open (esp. operational-environmental envelope)

**CONCERN.** The operational envelope is present, not silent: a Deployment/environments section with diagram, Local + one hosted environment `[ASSUMPTION]`, per-module migrations as a release step, container image + managed/container Postgres, GitHub Actions pipeline stages, config/secrets via env vars, structured JSON logging in the conventions table. Good coverage for a v1 learning project.

Gaps:

- **(MEDIUM) Observability is under-owned in the spine.** `.memlog.md` records "structured JSON logging + a health endpoint; OpenTelemetry deferred" — but the spine body has no health endpoint and no OpenTelemetry/telemetry entry in the Deferred list. "Monitoring/tracing beyond structured logs" is effectively silent. Add the health endpoint to the API surface and an explicit Deferred bullet for telemetry.
- **(LOW)** No mention of DB backup/restore or migration rollback, even to defer them.
- **(LOW)** No API-versioning note, even to defer it (defensible for a single-origin generated client, but say so).

Non-operational dimensions are decided or explicitly deferred: security model (auth, CSRF, hashing, validation, authz), testing strategy (arch + unit + integration via Testcontainers + e2e), error handling (ProblemDetails), API/data conventions, frontend build/state. No whole dimension is left wholly silent.

### 7. Diagrams are valid mermaid and carry real structure

**PASS** (syntax reviewed by hand, not machine-rendered).

- **Dependency-direction `graph TD`** — nodes, solid vs `-.reads.->` dotted labeled edges, `classDef`/`class` for impl vs contract. Valid syntax; carries the spine's central rule ("any edge not shown is a boundary violation"). This diagram is doing real work.
- **Container/system `graph LR`** — `subgraph Deployable [Single container]`, `SPA --- API` undirected link, `DB[(...)]` cylinder. Valid; real structure (single deployable, same origin, one DB with three schemas).
- **`erDiagram`** — entities with attributes, dotted `||..o{` cross-module relationships, quoted attribute comments marking "id only — no FK". Valid; deliberately encodes the *absence* of FKs as the structural point.
- **Deployment `graph TD`** — two subgraphs (Local / Hosted), CI edge with a comma-list label. Valid; carries the environment split and pipeline.

No diagram is decorative; each encodes a decision. Recommend a render check in CI, but nothing here looks broken.

### 8. AD IDs stable/unique; frontmatter coherent

**CONCERN.**

- AD-1 through AD-18 — all present, sequential, unique, no collisions, no gaps. IDs are stable handles.
- **(LOW) Inconsistent status tagging.** Only AD-1, AD-2, AD-3 carry `[ADOPTED]`; AD-4..AD-16 carry none (some carry `(resolves PRD OQ-x)` parentheticals instead), AD-17/AD-18 none. `.memlog.md` shows all of them as decided/adopted, and frontmatter `status: draft`. Either tag them all or drop the three tags; as-is a reader can't tell whether AD-4 is less settled than AD-1.
- **(LOW) Cross-reference error.** The Design-Paradigm table (frontend "Bounded-context module" row) says "FSD slice group (piloted — see AD-18)". The FSD-mirrors-bounded-context pilot is **AD-17**; AD-18 is "anonymous browse; auth only to apply". Wrong pointer.
- Frontmatter otherwise coherent: `altitude: feature` matches the task framing; `binds` maps to real PRD FR/NFR ids; all five `sources` paths resolve on disk (PRD, brief, addendum, research, EXPERIENCE.md, DESIGN.md); `created`/`updated` consistent at 2026-09-05; `companions: []` fine.
- **(LOW)** `binds` lists FRs + NFRs but not SM-1/SM-2/SM-C1, though the body binds ADs to those metrics. Minor.

---

## Contradiction / consistency sanity check

- **PRD OQ-4 (email uniqueness):** AD-11 resolves it to per-role (two tables) and explicitly says "the PRD should be updated to match." Consistent with FR-1/FR-2 wording ("already registered as a Company" / "as a Job Seeker") and EXPERIENCE.md's role-toggle model. Not a hidden contradiction — a flagged forward-resolution. Track the PRD update.
- **PRD OQ-3 (anon browse):** AD-18 resolves it as the PRD assumed. Consistent.
- **EXPERIENCE.md apply-gate:** no hard contradiction with AD-8 (the register and the apply are two operations, and the UX's own partial-failure path confirms they're non-atomic) — but the spine is silent on the orchestration and its contract. See finding H2 / checklist item 1.
- **EXPERIENCE.md "Company name on postings" / "Applicant name in list":** consistent — spine routes both through Contracts, JobPosting carries no company-name copy.
- **No FR is left without a home in the Capability→Architecture Map.** FR-1..FR-7 and all three NFRs have rows. The unhomed items are UX surfaces without a numbered FR: "List my postings" (Company) — no slice, no row; "My Applications" (Job Seeker) — slice exists, no row.
- No conflict with DESIGN.md / EXPERIENCE.md non-architectural constraints (light-mode, desktop-first, WCAG floor) — architecture doesn't touch them.

---

## Findings (tiered)

### Critical
_None._ The spine discharges its stated purpose — CI-enforced module boundaries (AD-1/AD-2, SM-2) — concretely and well, and keeps ceremony down (SM-C1).

### High
- **H1 — PostgreSQL 17 fails the "verified-current 2026-09-05" claim.** PostgreSQL 18 has been GA since 2025-09-25 (18.6 current). Update to 18, or state and justify the conservative pin. (checklist 4)
- **H2 — The UX apply-gate compound flow (register → auto-submit application, with defined partial-failure) has no architectural home.** No decision on where orchestration lives or its contract; AD-8 implicitly pushes it client-side but the spine never says so. Real feature-altitude divergence point, missed. (checklist 1)

### Medium
- **M1 — AD-5 over-claims enforcement.** Cross-schema raw SQL through a module's own DbContext is not caught by AD-2's assembly-dependency tests; the clause is partly discipline-enforced. State it honestly or add a real check. (checklist 2)
- **M2 — Pagination contract half-specified.** AD-15 + EXPERIENCE.md require paged search results and applicants; param names, default/max size, and index base are undecided. (checklist 1)
- **M3 — Validation-error shape not pinned.** ProblemDetails is fixed; the field-level member the UX's inline per-field errors need is not. (checklist 1)
- **M4 — Observability under-owned.** Health endpoint and OpenTelemetry-deferral are in `.memlog.md` but absent from the spine body and Deferred list. (checklist 6)
- **M5 — AD-13 leaves Data Protection key persistence as "volume or DB".** Unresolved either/or across stories; tie it to the hosting decision or pick one. (checklist 3)
- **M6 — "List my postings" (Company) capability required by the UX has no slice in the source tree and no Capability→Architecture Map row.** (checklist 5)

### Low
- **L1 — Cross-reference error:** Design-Paradigm table says "see AD-18" for the FSD pilot; it is AD-17. (checklist 8)
- **L2 — Inconsistent AD status tags:** only AD-1/2/3 carry `[ADOPTED]`; the rest carry nothing though all are decided per memlog. (checklist 8)
- **L3 — "My Applications" (Job Seeker) list has a slice but no FR and no map row;** spine doesn't flag this PRD gap the way it flags OQ-3/OQ-4. (checklist 5)
- **L4 — Imprecise Stack rows:** ArchUnitNET / Testcontainers / NSwag lack the dated version the table header promises. (checklist 4)
- **L5 — No API-versioning note and no DB backup/rollback note,** even to defer them. (checklist 6)
- **L6 — `binds` frontmatter omits SM-1/SM-2/SM-C1** though the body binds ADs to them. (checklist 8)
- **L7 — SM-1 says "deployed and usable" while hosting platform is deferred/undecided** — acknowledged tension, acceptable for planning. (checklist 3)

---

## Bottom line

A tight, purpose-focused spine: it spends its words on the one thing NexusJob is practising (enforced module boundaries) and resists CRUD ceremony, exactly as SM-2 / SM-C1 demand. The boundary ADs (AD-1, AD-2) are genuinely enforceable and named. Fix H1 (Postgres version) and H2 (apply-gate flow) before deriving epics, close the MEDIUM contract/operational gaps (M1–M6), and sweep the LOW coherence nits (especially L1's wrong AD pointer). No critical structural defect; no FR left unhomed in the map.
