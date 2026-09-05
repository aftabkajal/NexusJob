---
title: PRD: NexusJob
status: final
created: 2026-09-05
updated: 2026-09-05
revisions:
  - 2026-09-05 — Open Questions 3 & 4 resolved by the architecture spine (AD-18, AD-11); assumptions index annotated.
---

# PRD: NexusJob

## 0. Document Purpose

This PRD defines the v1 scope of NexusJob for the builder (acting as PM, architect, and developer) and for the downstream architecture and implementation workflows that follow it. Requirements are grouped by feature with globally numbered FRs (FR-1…FR-7) so later artifacts can reference them by stable ID. Technical/architectural decisions (module boundaries, tooling, frameworks) are already captured in the accompanying [Product Brief](../../briefs/brief-NexusJobBmad-2026-09-05/brief.md) and its addendum — this document does not repeat them, only the capabilities they must support.

## 1. Vision

NexusJob is a minimal two-sided job application platform: companies post jobs, job seekers search and apply. Functionally that is the entire product, deliberately — the job board itself is not the point.

The actual deliverable is a practice vehicle for AI-assisted, spec-driven development: taking a system from brief through architecture through implementation, on a domain simple enough that the process — not the feature list — stays the visible thing. The domain is intentionally small but structurally real: two actors, real state transitions (posted → viewed → applied), and enough natural seams to justify splitting into bounded contexts, which is exactly the exercise this project is for.

NexusJob does not aim to differentiate as a product — a generic job board already has better-resourced competitors (LinkedIn, Indeed, and dozens more), and that's the honest answer rather than a fabricated one. If there is a "different" here, it's in how it's built, not what it is.

Success is not adoption or revenue. It's a working core loop, module boundaries that are enforced rather than described, and a builder who can point to the artifacts and explain the reasoning behind them.

## 2. Target User

### 2.1 Jobs To Be Done

- **The builder (primary):** gain working fluency in AI-assisted spec-driven development and in applying DDD / modular-monolith / Vertical Slice Architecture to a real system, by making the boundary decisions and living with the consequences — not by reciting the theory.
- **Companies (in-product role):** post a job, see who applied. Nothing more.
- **Job Seekers (in-product role):** find a job, see enough detail to decide, apply. Nothing more.

*Company and Job Seeker are minimal personas sized to what the core loop requires — not researched or validated user segments.*

### 2.2 Key User Journeys

- **UJ-1.** Raj, a hiring manager at a small startup, logs in, posts a job opening in a couple of minutes, and later comes back to see the list of people who applied.
- **UJ-2.** Amara, job-hunting, searches NexusJob by keyword, opens a posting that looks right, and applies, needing only to confirm her account.

## 3. Glossary

- **Company** — An account that can create Job Postings and view Applications submitted to them.
- **Job Seeker** — An account that can search Job Postings and submit Applications.
- **Job Posting** — A single job opening created by a Company; has a title and description, and is searchable by Job Seekers once created.
- **Application** — A record that a specific Job Seeker applied to a specific Job Posting. At most one Application per (Job Seeker, Job Posting) pair.
- **Applicant** — A Job Seeker who has submitted an Application to a given Job Posting, from the Company's point of view.

## 4. Features

### 4.1 Account & Authentication

**Description:** Both roles need a basic account to use the product. Sign-up and sign-in are plain email and password per role — no SSO, no email verification, no admin approval of accounts (confirmed). Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-1: Company Sign Up / Log In

A Company can register an account with a unique email and password, and sign in with those credentials.

**Consequences (testable):**
- Registration fails if the email is already registered as a Company.
- Sign-in succeeds only with a matching email/password pair; otherwise it is rejected without revealing which field was wrong.
- No email verification or approval step gates account activation — the account is usable immediately after registration.

#### FR-2: Job Seeker Sign Up / Log In

A Job Seeker can register an account with a unique email and password, and sign in with those credentials.

**Consequences (testable):**
- Registration fails if the email is already registered as a Job Seeker.
- Sign-in succeeds only with a matching email/password pair; otherwise it is rejected without revealing which field was wrong.
- No email verification or approval step gates account activation.

### 4.2 Job Postings

**Description:** Companies create postings; Job Seekers search and view them. Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-3: Create Job Posting

An authenticated Company can create a Job Posting with at minimum a title and description.

**Consequences (testable):**
- A created Job Posting is immediately searchable and viewable by Job Seekers.
- A Job Posting is attributed to the Company that created it.
- Title and description must be non-empty. `[ASSUMPTION: no further length/format bound is specified — treat as an implementation default if one becomes necessary.]`

**Out of Scope:**
- Editing, deactivating, or deleting a posting after creation — see Open Questions.

#### FR-4: Search Job Postings

A Job Seeker can search Job Postings by keyword, matched against title and description text.

**Consequences (testable):**
- Search returns Job Postings whose title or description contains the keyword, via a case-insensitive substring match.
- No filtering by location, salary, or other facets is available.
- Search does not require an authenticated Job Seeker account. `[ASSUMPTION: only applying requires login — search and browsing are open. Confirm if search should also require an account.]`

#### FR-5: View Job Posting Details

A Job Seeker can open a Job Posting from search results to view its full details.

**Consequences (testable):**
- Displays at minimum: title, description, and the name of the Company that posted it.
- Viewing does not require an authenticated Job Seeker account (same assumption as FR-4).

### 4.3 Applications

**Description:** The loop closes here — a Job Seeker applies, a Company sees who applied. Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-6: Apply to a Job Posting

An authenticated Job Seeker can apply to a Job Posting.

**Consequences (testable):**
- Applying requires an authenticated Job Seeker account.
- At most one Application is recorded per (Job Seeker, Job Posting) pair; a repeat attempt does not create a duplicate. `[ASSUMPTION: duplicate applications to the same posting are prevented, not just allowed to pile up.]`
- The Job Seeker receives confirmation that the Application was submitted.

**Out of Scope:**
- Resume/cover-letter attachment, withdrawing an application, or any status beyond "applied" (confirmed out of scope for v1).

#### FR-7: View Applicants for a Posting

An authenticated Company can view the list of Job Seekers who applied to a Job Posting it owns.

**Consequences (testable):**
- The list shows, at minimum, each Applicant's identifying info (for example, name/email) and the Application timestamp.
- A Company can only view Applicants for Job Postings it created — not other Companies' postings.
- No scoring, ranking, filtering, or status workflow is applied to the list (confirmed out of scope).

## 5. Non-Goals (Explicit)

- NexusJob is not competing for job-board market share and will not grow features toward that goal.
- It is not an applicant tracking system (ATS) — no interview stages, offers, rejections, or hiring workflow.
- It does not support multi-tenant company accounts (teams/roles within one company).
- It is not a mobile app — web only.

## 6. MVP Scope

### 6.1 In Scope

- Company: sign up / sign in, create a Job Posting, view the list of Applicants for its own posting(s).
- Job Seeker: sign up / sign in, search Job Postings by keyword, view a posting's details, apply to a posting.
- Module/bounded-context boundaries enforced in CI (see §7 Cross-Cutting NFRs).

### 6.2 Out of Scope for MVP

- Resume upload/parsing, AI-based matching or screening.
- Application status tracking beyond "applied."
- Messaging or notifications (email, in-app, or otherwise).
- Search filters beyond keyword (location, salary, etc.).
- Editing, deactivating, or deleting a Job Posting after creation. `[NOTE FOR PM: revisit if the core loop feels incomplete without it — see Open Questions.]`
- Admin/moderation tooling, content review, abuse handling.
- Payments, subscriptions, or paid listings.
- SSO, email verification, or admin approval of accounts.

## 7. Cross-Cutting NFRs

- **Module boundary enforcement:** The build must fail if any module accesses another module outside its published contract surface — enforced by an automated architecture test, not by developer discipline alone. This is a primary success criterion for the project, not an incidental quality attribute.
- **Credential security:** Passwords are hashed at rest; never logged or stored in plaintext.
- **Platform:** A single web application. Responsive design for desktop and mobile is not required, and there is no native app.

## 8. Success Metrics

**Primary**
- **SM-1**: The full core loop (post → search → view → apply → view applicants) works end-to-end, deployed and usable. Validates FR-1 through FR-7.
- **SM-2**: Module/bounded-context boundaries are enforced in CI, not just documented. Validates the NFR in §7.

**Secondary**
- **SM-3**: The builder can articulate, after the fact, what the modular-monolith/DDD/Vertical-Slice approach cost and bought on this system.
- **SM-4**: The spec-driven workflow itself — brief → PRD → architecture → implementation — produces artifacts the builder can point to and explain, including the reasoning behind decisions. Evidenced by this PRD, its source brief/addendum, and the workspace's decision log, not by a specific FR.

**Counter-metrics (do not optimize)**
- **SM-C1**: Layering/ceremony added beyond what the genuinely CRUD-shaped core loop needs. The addendum's own research warns against applying full Clean Architecture ceremony where Vertical Slice suffices — more architecture is not the win condition. Counterbalances SM-2.

## 9. Open Questions

1. Can a Company edit, deactivate, or delete a Job Posting after creating it, or is v1 strictly create-and-list with no further lifecycle? *(Still open — a product call. The architecture keeps posting mutation solely inside the JobPostings module so adding lifecycle later is contained.)*
2. What minimum applicant fields (beyond email) does a Company need to see in the Applicant list for it to be meaningful — is a name required at sign-up? *(Resolved by the UX spec: a name is collected at sign-up — "Company name" for a Company, "Full name" for a Job Seeker — and the Applicant list shows name, email, and application timestamp.)*
3. Should browsing/searching Job Postings require a Job Seeker account, or stay open as assumed in FR-4/FR-5? *(**Resolved** — stays open. Architecture spine AD-18: search and posting-detail endpoints are anonymous; only apply / create-posting / view-applicants require a session.)*
4. Is email uniqueness scoped per role (the same email could register as both a Company and a Job Seeker), or must it be globally unique across both roles? This is a data-model decision (one identity table vs. two) that FR-1/FR-2 don't currently resolve. *(**Resolved** — per-role. Architecture spine AD-11: separate `CompanyAccount` and `JobSeekerAccount` tables, email unique within each, not across both; the same address may hold one account of each kind as two unrelated identities. This matches FR-1/FR-2's "already registered as a Company" / "as a Job Seeker" wording.)*

## 10. Assumptions Index

- §4.2 (FR-4, FR-5) — Search and viewing Job Posting details do not require an authenticated Job Seeker account; only applying does. *(Confirmed by architecture spine AD-18.)*
- §4.3 (FR-6) — Duplicate Applications to the same posting by the same Job Seeker are prevented, not merely allowed to accumulate. *(Confirmed — architecture spine AD-4/AD-20: a DB unique constraint on `(job_posting_id, job_seeker_id)`; a repeat attempt is idempotent, not an error.)*
- §4.2 (FR-3) — Title and description have no length/format bound beyond non-empty; further validation is an implementation default if needed.
- §4.1 (FR-1, FR-2) — Email uniqueness is scoped per role, not global (see Open Question 4 resolution). Architecture spine AD-11.
