---
title: Adversarial Divergence Review — ARCHITECTURE-SPINE.md (NexusJob v1)
target: ../ARCHITECTURE-SPINE.md
reviewer-role: adversarial divergence reviewer
date: 2026-09-05
sources:
  - ../ARCHITECTURE-SPINE.md
  - ../../../prds/prd-NexusJobBmad-2026-09-05/prd.md
  - ../../../ux-designs/ux-NexusJobBmad-2026-09-05/EXPERIENCE.md
---

# Adversarial Divergence Review

## Verdict

The spine is strong on the one thing it set out to govern — enforced module boundaries — but it under-specifies the **shared data shapes and cross-module read/write choreography** that two builders must agree on without talking. At least three Critical holes let two teams each honour every AD to the letter and still ship parts that will not integrate: the apply-gate cross-module sequence (AD-8 never says who orchestrates it), the Identity Contract surface (AD-4 lists entity columns, not the projection other modules consume, and contradicts its own ER diagram), and the duplicate-apply response contract (AD-4 pins where the constraint lives, not what the API returns or what happens on the race).

## Method

For each hole: two concrete units one level below the spine (two slices, backend vs frontend, two features), the divergent choice each legitimately makes, the AD that should have forestalled it, and a proposed new/tightened AD. Tiered list at the end.

---

## H1 — Critical — The apply-gate cross-module sequence has no named orchestrator; AD-8 is ambiguous on "user action"

**The two units**

- **Unit A — `frontend/src/features/apply-to-posting/`** (the AD-17 pilot slice). Implements EXPERIENCE Flow 2 / `apply-gate-modal` as **two sequential calls from the generated client**: `POST /api/auth/register` (Identity issues the session cookie), then `POST /api/job-postings/{id}/applications` (Applications writes the row). The "pending application" and the partial-failure retry (account created, apply failed) are held in the feature's `model/` segment client-side. Reading of AD-8: "one *HTTP request* mutates one module."
- **Unit B — `NexusJob.Modules.Applications/Features/Apply/`**. Built to also accept an anonymous-with-credentials payload: `POST /api/applications` with `{ jobPostingId, newAccount: { fullName, email, password } }`. The handler calls `IIdentityApi.RegisterJobSeeker(...)` then writes the `Application` in the same request, for "atomicity." Reading of AD-8: "one *user gesture* — the modal submit — and it still touches one module's `SaveChanges` for the Application; the account creation is delegated through a Contract, which AD-6 explicitly permits."

**The divergence**

Endpoint contract differs (two endpoints vs one combined endpoint). Failure semantics differ (client holds `pendingPostingId` and retries the apply call vs the combined endpoint must define its own partial-commit behaviour — account created, application not — which is precisely the AD-8-forbidden cross-module partial write, now smuggled in through a Contract call). Session-issuance timing differs: in A the cookie exists before the apply call; in B the combined handler must issue the cookie mid-request — and **nothing in the spine says only Identity's `/api/auth/*` endpoints may call `HttpContext.SignInAsync`**, nor does any Contract expose "issue a session," so Unit B either invents a Contract method the spine never sanctioned or calls `SignInAsync` from inside Applications.

**Missing / weak AD**

AD-8 ("one user action mutates exactly one module") never defines *user action* — gesture vs HTTP request — and the Capability Map row for FR-6 points only at `Applications · features/apply-to-posting` without pinning that the anonymous-apply journey is composed on the client. The Deferred "Domain events / async messaging — revisit when a write must fan out across modules" does not acknowledge that the apply-gate **already is** a two-module fan-out, so a builder may instead reach for the deferred event bus.

**Proposed fix**

New AD: *"The apply-gate is orchestrated by the frontend as two ordered API calls — `POST /api/auth/register`, then the apply call — with no combined endpoint. A multi-module user journey is always composed client-side; AD-8 is restated as **one HTTP request mutates exactly one module's schema**. Session issuance (`SignInAsync`) happens only inside Identity's `/api/auth/*` endpoints and is never exposed on a Contract or invoked from another module. The 'account created, apply failed' state is held client-side and retried against the apply endpoint (EXPERIENCE failure path)."* Add a sentence to the Deferred events bullet noting the apply-gate is handled by client orchestration, not events.

---

## H2 — Critical — The Identity Contract projection is undefined, and AD-4 contradicts the ER diagram on the name field

**The two units**

- **Unit A — `NexusJob.Modules.Applications/Features/ListApplicants/`** (FR-7). Needs "each Applicant's identifying info (for example, name/email) and the Application timestamp" (PRD FR-7). Designs its dependency as `IIdentityApi.GetJobSeeker(Guid id) : JobSeekerDto { Guid Id; string FullName; string Email; }` — `FullName` because the ER diagram says `JOB_SEEKER_ACCOUNT { string full_name }` and EXPERIENCE says the sign-up field is "Full name"; `Email` because FR-7 requires it.
- **Unit B — `NexusJob.Modules.Identity/`** contract authors. Build `IIdentityApi.GetAccount(Guid id) : AccountDto { Guid Id; string DisplayName; AccountType Type; }` — one polymorphic method for both account types, `DisplayName` because **AD-4's prose says both `CompanyAccount` and `JobSeekerAccount` carry "display name"**, and **no `Email`** because a privacy-minded Identity dev decides email is credential-adjacent PII that should not cross a module boundary (AD-13: "Password material is never written to logs" read expansively), and FR-7's "for example, name/email" is non-binding.

**The divergence**

Method shape (`GetJobSeeker` vs polymorphic `GetAccount`), field name (`FullName` vs `DisplayName`), and — the load-bearing one — **whether email crosses the boundary at all**. If B ships, FR-7's Applicants list cannot show email and EXPERIENCE Flow 1 step 9 ("real names, emails, and application timestamps — not a placeholder") is unsatisfiable. The same clash recurs for FR-5 company name: JobPostings' `GetPosting` slice calls `IIdentityApi.GetCompany(id).Name` while Identity exposes `.DisplayName` on a polymorphic DTO — or exposes a method that does not exist.

**Missing / weak AD**

AD-4 enumerates *entity columns* ("id, email, password hash, display name") but never the *Contract projection* the other two modules consume, and it internally disagrees with the spine's own ER diagram (`display_name` for company, `full_name` for job seeker) and with EXPERIENCE ("Full name" for Job Seeker). The Consistency Conventions row "Contract naming | `I{Context}Api` … DTOs suffixed `Dto`" fixes the *names of the types* but not their *fields*.

**Proposed fix**

New AD pinning `IIdentityApi`: `GetCompany(Guid) : CompanyDto { Guid Id; string Name; }`, `GetJobSeeker(Guid) : JobSeekerDto { Guid Id; string Name; string Email; }`, plus a batch `GetJobSeekers(IReadOnlyList<Guid>) : IReadOnlyList<JobSeekerDto>` and `GetCompanies(...)` for list rendering (see H8). State explicitly: *"`Email` is exposed cross-module on `JobSeekerDto` solely to satisfy FR-7; no password material, hash, or auth state is ever on a Contract DTO."* Pick one term — "Name" — and reconcile AD-4 prose, the ER diagram, and EXPERIENCE to it.

---

## H3 — Critical — One-application-per-pair: response contract and race behaviour unpinned

**The two units**

- **Unit A — `NexusJob.Modules.Applications/Features/Apply/` (variant: pre-check)**. Handler does `SELECT … WHERE job_posting_id = @p AND job_seeker_id = @s`; if a row exists, returns **`200 OK` with the existing `ApplicationDto`** (idempotent apply). Relies on the pre-check, not on catching the constraint.
- **Unit B — same slice (variant: insert-and-catch)**. Handler just `INSERT`s; on Postgres `23505` unique-violation returns **`409 Conflict` ProblemDetails** ("You have already applied to this posting").

**The divergence**

Both honour AD-4 ("`UNIQUE (jobPostingId, jobSeekerId)` … lives here and nowhere else") — the constraint is in Applications either way. But the API contract is incompatible: `200`+body vs `409`+ProblemDetails. The frontend `{components.apply-button}` must relabel to a disabled "Applied" state on a repeat attempt (EXPERIENCE) and needs to know which status means "already applied." Worse, **Unit A has an unhandled race**: two concurrent applies both pass the pre-`SELECT`, one `INSERT` wins, the other throws `23505` and — with no catch — surfaces as `500` via the `exception → ProblemDetails` middleware (AD-14). So even "obeying" AD-4, the un-pinned choice yields a 500 on the documented one-per-pair path.

Related sub-hole — **who serves the button's initial disabled state**. EXPERIENCE: "Signed-in Job Seeker who already applied: the button renders directly in the disabled 'Applied' state **on load**." That requires posting-detail load to know `hasApplied` for the current principal. `GET /api/job-postings/{id}` is a JobPostings slice; for it to return `hasApplied` it would have to call `IApplicationsApi` — but the dependency graph shows **no `JobPostings → Applications.Contracts` edge**, so AD-2 fails the build. The only legal source is an Applications endpoint, and the spine never says so, so:

- **Unit C — `frontend/src/features/apply-to-posting/api/`** calls a dedicated `GET /api/applications/mine` and cross-references `postingId` client-side.
- **Unit D — `frontend/src/pages/posting-detail/`** (a JobPostings-concern surface, "organised by frontend need" per AD-17) expects `hasApplied` on the `GET /api/job-postings/{id}` payload and files a backend ask; a JobPostings dev satisfies it by a raw cross-schema `SELECT` into `applications.application` — breaking AD-5 — or by adding an `IApplicationsApi` reference — breaking AD-2.

**Missing / weak AD**

AD-4 pins the *location* of the constraint, not the *enforcement path* (pre-check vs DB catch), the *race outcome*, the *idempotent-vs-conflict response*, or the *read path for `hasApplied`*. FR-6's "a repeat attempt does not create a duplicate" and "receives confirmation" are silent on status codes.

**Proposed fix**

New AD: *"Apply is idempotent. The handler attempts the insert and catches the unique-violation; a repeat attempt (including the concurrent race) returns `200 OK` with the existing `ApplicationDto` — never `409`, never `500`. Apply-eligibility (`hasApplied`) for a (seeker, posting) pair is served only by Applications, via `GET /api/job-postings/{id}/my-application` (`200` with the application, or `404`). JobPostings never references `Applications.Contracts`; posting-detail + `hasApplied` are composed on the client."*

---

## H4 — High — `accountType` claim is named; `accountId` acquisition is not

**The two units**

- **Unit A — `NexusJob.Modules.JobPostings/Features/CreatePosting/`**. Reads the owner id from `User.FindFirstValue(ClaimTypes.NameIdentifier)`, writes it to `owner_company_id`.
- **Unit B — `NexusJob.Modules.Applications/Features/Apply/`**. Assumes the cookie carries only `accountType` (that is literally all AD-13 names) plus an `email` claim, and resolves the seeker id by calling `IIdentityApi.GetJobSeekerByEmail(email)`.

**The divergence**

If Identity's `SignIn`/`Register` slices (Unit C) put only `accountType` + `sub = email` into the principal and **not** the account id as `NameIdentifier`, Unit A silently writes `null`/empty owner ids and Unit B makes an extra Contract round-trip on every authenticated request. AD-9's ownership check ("compares [`ownerCompanyId`] to the caller's account id") then compares a `Guid` from the Contract against a `string` claim whose format (`"D"` vs `"N"`, casing) was never pinned — a subtle mismatch that passes unit tests with hand-built principals and fails in integration.

**Missing / weak AD**

AD-13 names only the `accountType` claim. AD-11 says sign-in "resolves to `(accountType, accountId)`" but that tuple never reaches the wire format. No single accessor is mandated, so every slice invents its own "who am I."

**Proposed fix**

Tighten AD-13: *"The auth cookie carries exactly two claims: `sub` = accountId as a lowercase canonical UUID string, and `account_type` ∈ `{company, job_seeker}`. Every slice obtains the current principal's id through one shared `ICurrentAccount { Guid Id; AccountType Type }` accessor registered by the Host; slices never read `ClaimsPrincipal` directly. Ownership comparisons are `Guid`-typed."*

---

## H5 — High — AD-15 names the pagination page object but never defines it

**The two units**

- **Unit A — `NexusJob.Modules.JobPostings/Features/SearchPostings/`** returns `{ items: T[], page: 1, pageSize: 20, totalCount: 137 }` — offset, 1-based.
- **Unit B — `NexusJob.Modules.Applications/Features/ListApplicants/`** returns `{ data: T[], meta: { perPage: 20, nextCursor: "…", hasMore: true } }` — cursor, nested, different key names.

**The divergence**

Both satisfy AD-15 ("a small typed page object (`items` + paging fields)"). The generated TS client (AD-15, NSwag) now emits two unrelated page types; the frontend cannot build the one `Paginated<T>` helper that AD-16's `shared/` layer wants, and the single `Pagination` component EXPERIENCE mandates on **both** Search results and the Applicants list has to special-case each. Cursor pagination also quietly contradicts EXPERIENCE's "Pagination, not infinite scroll … page-at-a-time" — a backend dev optimising in isolation would not know.

**Missing / weak AD**

AD-15 defers the actual shape ("paging fields" unspecified) and the request contract (`?page=`/`?pageSize=`, defaults, max).

**Proposed fix**

New AD or AD-15 addendum: *"`PageDto<T> { T[] Items; int Page; int PageSize; int TotalCount; int TotalPages; }`, 1-based, offset pagination only in v1, identical field names on every list endpoint. Request: `?page` (default 1) and `?pageSize` (default 20, max 100). No cursor pagination in v1."*

---

## H6 — High — FR-7 "not found vs blocked" is pinned for the API (AD-9) but not for the frontend, and zero-applicants is unpinned

**The two units**

- **Unit A — `frontend/src/features/view-applicants/ui/`**. On `404` from `GET /api/job-postings/{id}/applicants` renders EXPERIENCE's generic "This posting is no longer available."
- **Unit B — same feature, different dev**. Reads a `404` reached *from the Company's own My Postings list* as "must be a stale row / access problem" and renders "You don't have access to this posting." — re-introducing at the UI the exact "blocked" confirmation AD-9 deleted at the API.

Plus: **Unit C — `NexusJob.Modules.Applications/Features/ListApplicants/`** returns `404` when the owned posting has **zero** applicants (conflating "empty" with "not found"), making EXPERIENCE's distinct "No applicants yet." state unreachable; **Unit D** returns `200` + empty page for that case. Both plausible; unpinned.

**The divergence**

AD-9 makes existence and ownership indistinguishable *in the API response* but says nothing about the frontend, which can leak the distinction back through copy choices; and "owned posting, no applicants" has no pinned status code, so the empty-state copy EXPERIENCE specifies may never render.

**Missing / weak AD**

AD-9 stops at the API boundary. EXPERIENCE pins the "Permission denied" treatment for the *My Postings* surface but not a rule that *every* `404` on a posting resource, on *every* surface, gets one identical treatment.

**Proposed fix**

Extend AD-9: *"The frontend renders one identical 'not available' treatment for any `404` on a posting resource on every surface, and never derives 'permission' vs 'existence' from status or body. `GET …/applicants` returns `404` only for a missing-or-not-owned posting; an owned posting with no applicants returns `200` with an empty page."*

---

## H7 — Medium — Company name on list rows: presence, and N+1 vs batch resolution

**The two units**

- **Unit A — `NexusJob.Modules.JobPostings/Features/SearchPostings/`** returns list items `{ id, title, snippet }` with **no company name** — AD-18 scopes search to a DB substring match "inside JobPostings … no search engine," and per-row cross-module calls read as scope creep.
- **Unit B — `frontend/src/widgets/search-results/`** builds `{components.job-card}` expecting `companyName` per row (the composition mock `key-home-search.html` shows it) and cannot render without it.

Sub-divergence: if company name *is* added to list items, `SearchPostings` either issues one `IIdentityApi.GetCompany` per row (N+1 in-process calls per page) or needs a batch method the Contract does not define (H2). And on posting *detail*, if `IIdentityApi.GetCompany(staleId)` returns null for an existing posting, one `GetPosting` variant `500`s, another renders a null name — unpinned.

**Missing / weak AD**

FR-5 pins company name on *detail* only; nothing says whether list rows carry it, how JobPostings resolves names in bulk, or what a missing company on a live posting does.

**Proposed fix**

Decide and pin: list rows **do** carry `companyName`; JobPostings resolves it via a batch `IIdentityApi.GetCompanies(ids)` (one call per page); a missing company on an existing posting renders a stable placeholder ("Company unavailable"), never a `5xx`.

---

## H8 — Medium — Contract id types (Guid vs string) unpinned

**The two units**

- **Unit A — `IJobPostingsApi.GetPosting(Guid id) : PostingDto { Guid Id; Guid OwnerCompanyId; … }`** — natural in-process C#.
- **Unit B — `NexusJob.Modules.Applications/Features/ListApplicants/`** works in `string` ids end to end (because the Consistency Conventions say ids are "exposed in the API as strings") and expects `IJobPostingsApi.GetPosting(string id) : PostingDto { string Id; … }`.

**The divergence**

The Contract can carry only one signature; two teams spec it two ways before integration. Feeds AD-9's format-mismatch risk (H4).

**Missing / weak AD**

Consistency Conventions pin the *wire* representation ("strings") but not the *in-process Contract* representation.

**Proposed fix**

Consistency Conventions addendum: *"Contract interfaces and DTOs use `Guid` for all ids; `string` conversion happens only at the HTTP edge (request binding and response serialisation)."*

---

## H9 — Medium — AD-16/AD-17 leave frontend feature structure, generated-client touch-point, and query-key ownership unpinned

**The two units**

- **Unit A — `frontend/src/features/apply-to-posting/`** (AD-17 pilot). Imports the generated client only inside `api/`; wraps it in typed TanStack hooks there; `model/` holds client-only state; `ui/` is presentational.
- **Unit B — `frontend/src/features/search-postings/`** ("organised by frontend need" per AD-17). Calls the generated client directly from `ui/` components; puts server-cache config in a `pages/` loader; adds `entities/posting/` with a hand-written `PostingView` type because "the generated DTO is ugly."

**The divergence**

Both pass the ESLint downward-import rule (AD-16), so CI is green, yet there is no shared convention for *where* the generated client is touched, *where* query keys and server-state hooks live, or whether `entities/*` may restate a generated DTO (AD-15 forbids "hand-written request/response types that duplicate the API"; Unit B argues a "view model" is not a "duplicate"). When the AD-17 checkpoint asks "propagate the mapping or not," there is no consistent baseline to judge. Separately, `features/apply-to-posting` and `features/view-my-applications` both read "my applications"; if each owns its own query key (`['applications','mine']` vs `['my-applications']`), applying does not invalidate the other's cache and EXPERIENCE Flow 2 step 8 ("Later, she opens My Applications and sees the posting listed") can show stale.

**Missing / weak AD**

AD-17 defers the bounded-context mapping but also leaves *un-pinned the parts that do not depend on it*. AD-16 pins layer direction and "TanStack Query, no global store" but not query-key ownership or the client touch-point.

**Proposed fix**

New AD (applies regardless of the AD-17 outcome): *"The generated client is imported only within a slice's `api/` segment. TanStack Query keys are defined once per server resource in `shared/api/query-keys` (or the owning `entities/*`) and imported by features; a mutation invalidates by resource key. `model/` is client-only state. No `entities/*` or feature may declare a type that mirrors a generated DTO. `pages/` and `widgets/` never call the generated client directly."*

---

## H10 — Medium — Antiforgery/CSRF scheme unpinned; the generated client will not attach a token

**The two units**

- **Unit A — Host antiforgery config**: ASP.NET Core default, header `RequestVerificationToken`, expects the SPA to fetch a bootstrap token.
- **Unit B — `frontend/src/shared/api/` interceptor**: reads an `XSRF-TOKEN` cookie and sends `X-XSRF-TOKEN` (common React pattern); no backend endpoint issues that cookie and the header name differs.

**The divergence**

Both "require an antiforgery token" per AD-13; every state-changing request `400`s. The NSwag-generated client (AD-15) attaches nothing by default, so *someone* must wrap it, and the spine does not say who or how.

**Missing / weak AD**

AD-13 mandates the token but not the scheme: cookie name, header name, the bootstrap endpoint, and the client-side interceptor location.

**Proposed fix**

Tighten AD-13: *"CSRF uses the cookie-token pattern: Host issues a non-`HttpOnly` `XSRF-TOKEN` cookie; state-changing requests must echo it in `X-CSRF-TOKEN`. A single interceptor in `shared/api` wraps the generated client to attach it. A `GET /api/antiforgery/token` endpoint seeds the cookie on first load."*

---

## H11 — Medium — Data Protection key storage is in Deferred, but it is an ownerless cross-cutting table whose choice depends on the undecided host

**The two units**

- **Unit A** configures `AddDataProtection().PersistKeysToFileSystem("/keys")` — correct for a single Docker host with a mounted volume.
- **Unit B** configures `PersistKeysToDbContext<TContext>()` — but *which* `DbContext`? Every module's `DbContext` is scoped to its own schema (AD-5), and DP keys are not Identity domain data, so this needs a Host-owned context/schema that no AD sanctions.

**The divergence**

AD-13 says keys are "persisted (volume or DB) so sessions survive restarts" but does not choose, and "Hosting platform undecided" is in Deferred. If the target turns out to be Azure Container Apps / Fly.io (ephemeral filesystem) and Unit A shipped, every deploy silently logs all users out — an SM-1 ("deployed and usable") regression discovered only in production.

**Missing / weak AD**

Deferred defers the hosting platform without noting that DP key persistence is coupled to it and has no module owner.

**Proposed fix**

New AD: *"Data Protection keys persist to a dedicated `data_protection` Postgres schema owned by `NexusJob.Host` (not a module), via a Host-scoped `DbContext`. This is host-agnostic and survives restarts on any candidate platform."*

---

## H12 — Low — Applicant list ordering must be server-side; timestamp field name unpinned

**The two units**

- **Unit A — `ListApplicants`** returns the page unordered and lets the frontend sort.
- **Unit B — `{components.applicant-row}`** relies on EXPERIENCE's "Rows sort by application timestamp, most recent first" and sorts client-side only.

**The divergence**

With pagination (H5), client-side sort only orders the current page; page 1 is not guaranteed to hold the newest applicants. Also the DTO field is `submittedAt` (AD-4) / `submitted_at` (ER diagram) / "Application timestamp" (PRD) — the JSON field name the generated client sees is unpinned.

**Proposed fix**

Pin: *"List endpoints sort server-side; Applicants sort `submitted_at DESC, id DESC` (stable). DTO field is `submittedAt`, ISO-8601 UTC."*

---

## Tiered findings

### Critical (integration will fail; fix before any slice is built)

| # | Hole | Missing/weak AD |
|---|---|---|
| H1 | Apply-gate cross-module sequence has no named orchestrator; "user action" undefined; session issuance not pinned to Identity | AD-8, AD-13, Capability Map FR-6 |
| H2 | Identity Contract projection undefined; AD-4 prose contradicts the ER diagram and EXPERIENCE on the name field; email-crosses-boundary unstated | AD-4, Consistency Conventions (Contract naming) |
| H3 | Duplicate-apply response contract + race outcome unpinned; `hasApplied` read path can only be legal via Applications but is never stated, inviting AD-2/AD-5 violations | AD-4, FR-6, dependency graph |

### High (real divergence; frontend and backend will disagree)

| # | Hole | Missing/weak AD |
|---|---|---|
| H4 | `accountId` never reaches the cookie claim set; no shared current-principal accessor; ownership-comparison format unpinned | AD-13, AD-11, AD-9 |
| H5 | Pagination page object named but shape and request contract undefined | AD-15 |
| H6 | FR-7 not-found-vs-blocked pinned for API only, not frontend; owned-posting-zero-applicants status code unpinned | AD-9, EXPERIENCE state table |

### Medium (contained but will cost rework)

| # | Hole | Missing/weak AD |
|---|---|---|
| H7 | Company name on list rows: presence + N+1 vs batch resolution + missing-company-on-live-posting behaviour | FR-5, AD-6, AD-18 |
| H8 | Contract id type (Guid vs string) unpinned | Consistency Conventions (Identifiers) |
| H9 | Generated-client touch-point, query-key ownership, `entities` DTO re-declaration unpinned; stale-cache risk vs EXPERIENCE | AD-16, AD-17, AD-15 |
| H10 | Antiforgery scheme (cookie/header names, bootstrap endpoint, client interceptor) unpinned | AD-13, AD-15 |
| H11 | Data Protection key storage deferred with the host; ownerless cross-cutting table | AD-13, Deferred |

### Low (worth a sentence)

| # | Hole | Missing/weak AD |
|---|---|---|
| H12 | Applicant sort must be server-side; timestamp DTO field name unpinned | AD-4, EXPERIENCE `{components.applicant-row}` |

### Cross-cutting observation

The spine's boundary machinery (AD-1, AD-2, AD-5, AD-6, AD-10, the dependency graph) is airtight and CI-enforced — an adversary cannot make two modules *couple* illegally without breaking the build. Every hole above is instead a **silence about a shared shape or a shared sequence**: the ADs govern *who may call whom*, not *what crosses the call* or *who drives a multi-call journey*. Closing H1–H3 (one new AD each) removes the three ways two conforming teams ship non-integrating halves; H4–H6 are one tightening sentence each on an existing AD.
