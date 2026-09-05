---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - prds/prd-NexusJobBmad-2026-09-05/prd.md
  - architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md
  - ux-designs/ux-NexusJobBmad-2026-09-05/DESIGN.md
  - ux-designs/ux-NexusJobBmad-2026-09-05/EXPERIENCE.md
---

# NexusJob - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for NexusJob, decomposing the requirements from the PRD, the UX Design contract (DESIGN.md + EXPERIENCE.md), and the Architecture Spine (22 ADs) into implementable stories.

The product is a deliberately minimal two-sided job board (companies post jobs, job seekers search and apply). Its purpose is a practice vehicle for AI-assisted, spec-driven development applying DDD / modular-monolith / Vertical Slice Architecture. **Enforced module boundaries in CI (SM-2) is a primary success criterion, not incidental quality.** Counter-metric SM-C1 penalises layering/ceremony beyond what the CRUD-shaped core loop needs.

## Requirements Inventory

### Functional Requirements

FR-1: A Company can register an account with a unique email and password and sign in with those credentials. Registration fails if the email is already registered as a Company. Sign-in succeeds only with a matching email/password pair and is otherwise rejected without revealing which field was wrong. No email verification or approval step gates activation - the account is usable immediately.

FR-2: A Job Seeker can register an account with a unique email and password and sign in with those credentials. Registration fails if the email is already registered as a Job Seeker. Sign-in succeeds only with a matching email/password pair and is otherwise rejected without revealing which field was wrong. No email verification or approval step gates activation.

FR-3: An authenticated Company can create a Job Posting with at minimum a title and description. A created posting is immediately searchable and viewable by Job Seekers, is attributed to the Company that created it, and requires non-empty title and description.

FR-4: A Job Seeker can search Job Postings by keyword, matched as a case-insensitive substring against title and description text. No filtering by location, salary, or other facets. Search does not require an authenticated account.

FR-5: A Job Seeker can open a Job Posting from search results to view its full details - at minimum title, description, and the name of the Company that posted it. Viewing does not require an authenticated account.

FR-6: An authenticated Job Seeker can apply to a Job Posting. At most one Application is recorded per (Job Seeker, Job Posting) pair; a repeat attempt does not create a duplicate. The Job Seeker receives confirmation that the Application was submitted.

FR-7: An authenticated Company can view the list of Job Seekers who applied to a Job Posting it owns. The list shows, at minimum, each Applicant's identifying info (name/email) and the Application timestamp. A Company can only view Applicants for postings it created. No scoring, ranking, filtering, or status workflow.

### NonFunctional Requirements

NFR-1 (Module boundary enforcement): The build must fail if any module accesses another module outside its published contract surface - enforced by an automated architecture test in CI, not by developer discipline alone. Primary success criterion (SM-2).

NFR-2 (Credential security): Passwords are hashed at rest; never logged or stored in plaintext.

NFR-3 (Platform): A single web application. Responsive design for desktop and mobile is not required; there is no native app. Light mode only for v1.

NFR-4 (Core loop end-to-end - SM-1): The full core loop (post -> search -> view -> apply -> view applicants) works end-to-end, deployed and usable.

NFR-5 (Anti-ceremony - SM-C1, counter-metric): No layering or ceremony added beyond what the genuinely CRUD-shaped core loop needs. More architecture is not the win condition.

### Additional Requirements

*From the Architecture Spine. No greenfield starter template is specified - the solution is hand-structured per the spine's source tree, so scaffolding is explicit work in Epic 1.*

AR-1 (Solution scaffold): Backend .NET 10 solution = `NexusJob.Host` + `NexusJob.Modules.{Identity,JobPostings,Applications}` + matching `.Contracts` projects + `NexusJob.ArchitectureTests`. Frontend = Vite 8 + React 19 + TypeScript with FSD layer folders (`app/ pages/ widgets/ features/ entities/ shared/`). The Host is the only project that references module implementations; modules reference only each other's `.Contracts`. (AD-1, AD-3, AD-10)

AR-2 (Architecture tests, build-breaking): ArchUnitNET tests fail the build when (1) a module impl assembly depends on another module's non-Contracts assembly, (2) a `.Contracts` assembly depends on any impl assembly, (3) any project but the Host depends on a module impl, (4) a `FromSqlRaw`/`ExecuteSql*` call carries a schema-qualified name outside the caller's own schema. Frontend: `eslint-plugin-boundaries` encoding the FSD layer order, also build-breaking. These tests are a primary deliverable. (AD-2)

AR-3 (Persistence): One PostgreSQL 18 database; schema-per-module (`identity`, `job_postings`, `applications`); one `DbContext` per module, mapping only its own entities with `search_path` set to its schema; no foreign key crosses a schema; per-module EF migration history; `Guid.CreateVersion7()` primary keys (string at the JSON edge); UTC ISO-8601 timestamps stored `timestamptz`; `data_protection_keys` table in `public`, owned by the Host. (AD-4, AD-5, AD-7, AD-22)

AR-4 (Inter-module communication): Synchronous in-process calls through `I{Context}Api` Contract interfaces resolved from DI - no event bus, no message broker. Contracts expose explicit `*Dto` types (never entities, no polymorphic account DTO), `Guid` ids in C# signatures. `IIdentityApi` provides `GetCompany`/`GetJobSeeker` and **batch** `GetCompanies(ids)`/`GetJobSeekers(ids)` returning a dictionary; `IJobPostingsApi` provides `GetPostingOwner(id)` and `GetPostingSummaries(ids)`. There is no `IApplicationsApi`. List projections must use the batch getters - a per-row Contract call is a defect. (AD-6, AD-19)

AR-5 (Auth mechanism): ASP.NET Core cookie authentication without the full ASP.NET Core Identity framework. Passwords hashed with `Microsoft.AspNetCore.Identity.PasswordHasher` (PBKDF2-HMAC-SHA256), `IterationCount` pinned explicitly to >= 600,000 (OWASP). One `HttpOnly; Secure; SameSite=Lax` cookie carrying `NameIdentifier` = account id (Guid) and an `account_type` claim (`company`|`job_seeker`); caller identity is read only from `NameIdentifier`. State-changing requests require an antiforgery token (double-submit, `X-CSRF-TOKEN` header, seeded by `GET /api/auth/csrf`). Credentials, hashes, and cookie values are never logged. (AD-13)

AR-6 (Same-origin hosting): `NexusJob.Host` serves the built React bundle as static files and the `/api` routes from one origin. No BFF proxy, YARP, Duende, or CORS handling. (AD-12)

AR-7 (HTTP contract shape): Errors are RFC 9457 ProblemDetails on every non-2xx. Success responses return the resource representation directly - no envelope. Every paginated list uses `Page<T> { items, page, pageSize, total }` - offset pagination, `page` 1-based, `pageSize` default 20 / max 100, request params `?page=&pageSize=`. REST paths are lowercase plural nouns and an endpoint's `/api` prefix follows its owning module: `/api/job-postings/*` (JobPostings), `/api/applications/*` (Applications, incl. FR-7 and apply-button state), `/api/auth/*` (Identity). (AD-15, conventions)

AR-8 (OpenAPI -> TypeScript client): The Host emits one OpenAPI 3.0 document (the .NET 10 generator is pinned to 3.0 output - NSwag's TS generator is unreliable on 3.1). The frontend TypeScript client is generated from it by NSwag in CI and is the only permitted description of the API on the client side; hand-written request/response types that duplicate the API are forbidden. Fallback logged: Orval, if the 3.0 pin proves lossy. (AD-15)

AR-9 (Frontend architecture): FSD layers `app -> pages -> widgets -> features -> entities -> shared`, imports only from layers below. Server state via TanStack Query, with query keys owned by the `entities/<noun>` slice and imported by features. Routing via React Router v8. No global client-state store in v1. The generated client (`shared/api`) is imported only from an `entities/*/api` or `features/*/api` segment. (AD-16)

AR-10 (FSD-mirrors-bounded-context pilot): The "FSD slice mirrors backend bounded context" mapping is adopted only as a pilot on the Applications capability, end to end, first. A checkpoint after that slice ships decides whether to propagate it. Until then, other frontend slices are organised by frontend concern. (AD-17)

AR-11 (Input validation): Built-in .NET 10 minimal-API validation (`AddValidation()`, DataAnnotations-based). Non-empty title and description enforced for FR-3. FluentValidation deferred. (AD-14)

AR-12 (Operability floor): `NexusJob.Host` exposes `GET /health` (process liveness + a `SELECT 1` DB probe). Structured JSON logging to stdout with credential/hash/cookie redaction. All config and secrets (DB connection string, Data Protection key-ring config, `PasswordHasher` iteration count) come from environment / .NET configuration - nothing secret in the image or repo. (AD-22)

AR-13 (CI pipeline): GitHub Actions - build, run architecture tests, unit tests, integration tests against a real Postgres (Testcontainers), one full-core-loop end-to-end test (SM-1), and build the container image. Frontend CI regenerates the OpenAPI TypeScript client and runs the `eslint-plugin-boundaries` check. (AD-2, AD-8/operational envelope, AR-8)

AR-14 (Deployment): A single container image (Host serving API + SPA build) plus one PostgreSQL instance. Docker Compose for local (`app` + `postgres`). Per-module migrations applied as a release step before the new image serves traffic. One hosted environment for v1; hosting platform undecided (decide before first deploy). (AD-12, AD-7, operational envelope)

AR-15 (Transaction boundary): One write request mutates exactly one module's schema in a single `SaveChanges` (register -> Identity, create posting -> JobPostings, apply -> Applications). No endpoint combines two modules' writes; no distributed transaction. (AD-8)

AR-16 (Apply-gate orchestration): Signed-out apply is two requests the SPA issues in order: `POST /api/auth/register` (Identity - creates the Job Seeker account and signs the cookie in one response), then on success `POST /api/applications` with body `{ jobPostingId }` (Applications). No server-side orchestration. Partial failure (register succeeds, apply fails) leaves the account created and signed in; the SPA re-issues only the second request. (AD-21)

AR-17 (Idempotent apply + already-applied read path): The Apply handler inserts and catches the Postgres unique-violation (`23505`), returning `200` with the existing application - never `409`, never `500`; no pre-check SELECT is used as the guard. The on-load Apply-button state comes from `GET /api/applications/mine?jobPostingId=<id>` (Applications, Job Seeker session), returning `{ applied, appliedAt }` or `{ applied: false }`. JobPostings never calls Applications. (AD-20)

AR-18 (FR-7 authorization + status codes): `GET /api/applications?jobPostingId=<id>` (Applications, Company session) resolves the posting's owner via `IJobPostingsApi.GetPostingOwner` and compares it to the caller's account id. Posting missing or not owned by the caller -> `404` with byte-identical ProblemDetails (never `403`). Owned with zero applicants -> `200` with an empty `Page<ApplicantDto>`. The frontend renders its "no longer available" not-found treatment for both `404` cases; there is no distinct "forbidden" screen. (AD-9)

AR-19 (Anonymous vs authenticated endpoints): Search (FR-4) and posting detail (FR-5) endpoints are `AllowAnonymous`. Only apply (FR-6), create-posting (FR-3), and view-applicants (FR-7) require an authenticated session. (AD-18)

AR-20 (Keyword search implementation): FR-4 is a case-insensitive substring match executed in the database (EF `Contains` / `ILIKE`) inside JobPostings - no search engine, no separate index service. (AD-18)

### UX Design Requirements

*From EXPERIENCE.md (behavior) and DESIGN.md (visual identity). The visual system is built from scratch - every token is this product's own contract, not a customization of an inherited library.*

UX-DR1 (Shared role-aware shell): One shell and navigation bar for both roles. What renders in it - nav items, primary actions - is role-aware based on whether the signed-in account is a Company or a Job Seeker; browsing/search stay open to signed-out visitors. There is no dead or disabled nav item pointing at a surface the current viewer cannot use.

UX-DR2 (Information architecture): Seven surfaces - Home / Search landing (root URL); Search results (paginated); Job posting detail; Sign up / Log in (role toggle); Post-a-Job form (authenticated Company only); My Postings + Applicants (authenticated Company only); My Applications (authenticated Job Seeker only). Post-a-Job and My Applications are spine-only (built from the tables in EXPERIENCE.md and DESIGN.md; no mockup).

UX-DR3 (job-card component behavior): Used on Search results and Home browse. Clicking anywhere on the card except the Apply button opens Job posting detail. Hover applies the elevation defined in DESIGN.md as the sole hover affordance. Contains one apply-button in the footer.

UX-DR4 (applicant-row component behavior): Used on My Postings -> Applicants view. Read-only - no click action, no per-row controls (no scoring/status per PRD scope). Rows sort by application timestamp, most recent first.

UX-DR5 (apply-button component behavior): Three states. Signed-out Job Seeker: click opens the apply-gate modal over the current posting. Signed-in Job Seeker who hasn't applied: click submits the application inline (no navigation) and the button relabels to a disabled "Applied" state. Signed-in Job Seeker who already applied: the button renders directly in the disabled "Applied" state on load. The same visual component serves as the "Publish" submit on Post-a-Job - it is the singular primary action per surface.

UX-DR6 (auth-role-toggle component behavior): Exactly one role active at a time; switching clears role-specific field errors but keeps entered email/password/name. Operable via Tab + arrow keys, Enter/Space to select. Inside the apply-gate modal the toggle is pre-set to Job Seeker and effectively fixed. Sign-up mode (not log-in) shows a required name field above email, labeled "Company name" or "Full name" per the active role; log-in mode never shows the name field. This name is the source of the Company name shown on postings and the Applicant name shown in the Applicants list.

UX-DR7 (apply-gate-modal component behavior): Opens as an interstitial over the same posting (no page navigation, no redirect), centered over a dimmed but still-visible posting detail. Opening moves focus to the first form field. On successful account creation inside the modal, the modal closes, the pending application auto-submits against the posting the visitor was already viewing, and the detail updates to the applied/confirmation state - no second Apply click. Escape or scrim click closes it, returns focus to the triggering Apply button, submits no application, and loses no data from the posting view. Focus is trapped inside the modal while open.

UX-DR8 (Voice and tone): Formal, professional microcopy - corporate-adjacent register. Complete sentences, terminal punctuation, no exclamation marks, no emoji, no exclamation-heavy startup voice. Prescribed strings include: "Your job posting has been published.", "Your application has been submitted.", "This email is already registered as a {Company/Job Seeker}.", "That email and password don't match. Please try again.", "We couldn't submit your application. Please try again.", "We couldn't run that search. Please try again.", "We couldn't publish this posting. Please try again.", "No postings match \"{keyword}.\" Try a different term.", "No open postings yet. Check back soon.", "This posting is no longer available.", "You haven't posted a job yet.", "No applicants yet.", "You haven't applied to anything yet."

UX-DR9 (State patterns - full matrix): Cold-load skeleton rows for Home browse, Search results (job-card shaped), Job posting detail (title/description/company block), My Postings (job-card), Applicants (applicant-row), My Applications. Empty catalog: "No open postings yet. Check back soon." (no error styling). No matches: "No postings match \"{keyword}.\" Try a different term." (no suggestions). Search/network error: "We couldn't run that search. Please try again." (retry re-submits same keyword). Posting not found: "This posting is no longer available." with a link back to Search. Apply-gate duplicate email: inline under email, "This email is already registered as a Job Seeker." (modal stays open). Apply-gate submit failure after account creation: modal closes (account exists), posting detail shows "We couldn't submit your application. Please try again." with retry - user not asked to re-enter credentials. Validation errors: inline, per-field, below the field. Registration duplicate email: inline under email, role-specific, other field values retained. Sign-in no match: generic inline error not revealing which field ("That email and password don't match. Please try again."). Publish success: "Your job posting has been published." then redirect to the new posting's detail view. Publish save failure: form values retained, inline banner "We couldn't publish this posting. Please try again.", retry re-submits same title/description. My Postings empty: "You haven't posted a job yet." with a link to Post-a-Job. Posting with zero applicants: "No applicants yet." (distinct copy from no-postings). Permission denied (Company navigating to another Company's posting id in this view): treated the same as not-found, never a "blocked" screen that confirms the posting exists. My Applications empty: "You haven't applied to anything yet." with a link to Search.

UX-DR10 (Interaction primitives): Submit-to-search, not search-as-you-type (single keyword field + explicit Search action; results refresh only on submit). Pagination, not infinite scroll, on both Search results and the Applicants list. Form validation on blur (per field) and again on submit, never on every keystroke; errors render inline below the field in the danger color and are associated with the field for assistive tech. Apply-gate modal open/close/focus behavior per UX-DR7.

UX-DR11 (Accessibility floor - WCAG 2.1 AA): Focus order follows visual reading order on every surface, including inside the apply-gate modal. Every action reachable by mouse is reachable and operable by keyboard alone. The auth-role-toggle is operable via Tab + arrow keys with Enter/Space. The apply-gate modal traps focus while open and restores focus to the triggering Apply button on close. Inline field errors are programmatically associated with their field (announced on focus or submit), not conveyed by color alone. Any Company logo image gets the Company name as alt text; purely decorative icons are marked decorative and hidden from assistive tech.

UX-DR12 (Key user flows): Flow 1 - a Company signs up (role toggle defaults to Job Seeker, selects Company), hits a duplicate-email failure path and switches to log in, posts a job, gets the publish confirmation, returns days later, opens My Postings, drills into a posting's Applicants view, and sees real names, emails, and timestamps. Flow 2 - a signed-out Job Seeker searches, hits a no-results near-miss and broadens the term, opens a posting, clicks Apply, completes the apply-gate modal (role pre-set to Job Seeker), the account is created, the modal closes, the application auto-submits against the same posting without a second click, and the confirmation appears on the posting still on screen; later they open My Applications and see the posting; failure path - the auto-submit fails transiently, the modal still closes, the detail shows the retry message, and a single retry resumes without re-entering credentials.

UX-DR13 (Design token system - built from scratch): Color tokens in the Nexus Indigo palette with AA-verified values (background #F5F6FB, surface #FFFFFF, primary #23215E, accent #0A7A90 [darkened from the reference swatch to clear AA on button text], text-primary #1A1B2E, text-secondary #5B5E78, border #DFE1F0, success #0B7A42 + subtle #E3F6EA, danger #C42744 + subtle #FBE4E8). Typography ramp in a single family (Inter): display 40/700, heading 28/700, heading-sm 20/600, body 16/400, body-sm 14/400, label 13/600 (+0.04em), caption 12/500. Rounding scale: sm 6, md 10, lg 16, xl 24, full 9999. Spacing scale on a 4px base (1-8) plus named gaps gutter 32 and editorial-gap 96. Elevation: resting cards flat with a border hairline only; job-card hover a soft lift; apply-gate modal a deep shadow over a scrim.

UX-DR14 (Layout rules): Content sits in a fixed desktop-first max-width container (1120px); no responsive breakpoints. Listings (search results, My Postings, My Applications) render as a single-column stack of cards at gutter spacing - never a dense multi-column table. Applicant rows are a simple vertical list, not a sortable-column table (the product has no ranking/filtering to expose).

UX-DR15 (Brand register and motion): Confident/modern visual register (bold, high-contrast) paired deliberately with the formal microcopy register. Light mode only for v1. Motion is subtle and purposeful only - fade/slide on state change, nothing decorative; respect reduced-motion.

UX-DR16 (Component visual specs): job-card (surface panel, lg radius, spacing.5 padding, heading-sm title, body-sm meta, one apply-button in footer), applicant-row (full-bleed row, border divider between rows, no per-row shell, name/email in body, timestamp right-aligned in caption, no action controls), apply-button (solid accent fill, label typography, md radius, disabled state uses border/text-secondary), auth-role-toggle (two-option pill, full radius track with border, active option primary fill), apply-gate-modal (surface panel, xl radius, spacing.6 padding, heading title, over scrim rgba(26,27,46,0.55), deep shadow).

UX-DR17 (Do's and Don'ts, load-bearing): Use the accent color only for the single primary action per surface (Apply, Publish, auth submit) - never for chrome, nav highlights, or decoration. Keep listings as single-column cards, never a dense table. One all-sans family across every type role. Subtle fade/slide transitions on state change only. Always pair success/danger color with a text label at AA contrast - never a color-only status signal. Reserve the full (pill) radius for the auth-role-toggle.

### FR Coverage Map

FR-1: Epic 1 - Company can register with a unique email/password and sign in; duplicate-Company-email rejected; generic sign-in error.
FR-2: Epic 1 - Job Seeker can register with a unique email/password and sign in; duplicate-Job-Seeker-email rejected; generic sign-in error.
FR-3: Epic 2 - Authenticated Company creates a Job Posting (non-empty title + description); immediately searchable/viewable; attributed to the creating Company.
FR-4: Epic 2 - Anonymous keyword search over title + description as a case-insensitive substring match; no facet filtering.
FR-5: Epic 2 - Anonymous full posting detail view including the posting Company's name (resolved via IIdentityApi).
FR-6: Epic 3 - Authenticated Job Seeker applies (incl. the signed-out apply-gate flow); at most one Application per (Job Seeker, Job Posting); confirmation shown.
FR-7: Epic 3 - Authenticated Company views the Applicant list (name/email + timestamp) for a posting it owns; not-owned/missing returns 404; no scoring/status.

## Epic List

### Epic 1: Accounts and the running skeleton
A Company and a Job Seeker can each register and sign in. The application is deployed and reachable, served same-origin (SPA + API from one origin), with module boundaries enforced by CI from the first commit. This epic stands up the modular-monolith scaffold (Host + three module projects + Contracts + ArchitectureTests), the Identity module, cookie authentication, the ArchUnitNET and eslint-plugin-boundaries build gate, the OpenAPI 3.0 -> NSwag TypeScript client pipeline, the `/health` endpoint, Docker Compose for local, and the GitHub Actions pipeline - all justified by delivering FR-1/FR-2, not built as a standalone "setup" epic. It also establishes the from-scratch design-token system and the role-aware application shell with the Sign up / Log in surface.
**FRs covered:** FR-1, FR-2
**NFRs / ARs carried:** NFR-1, NFR-2, NFR-3; AR-1, AR-2, AR-5, AR-6, AR-7, AR-8, AR-9, AR-11, AR-12, AR-13, AR-14, AR-15
**UX-DRs carried:** UX-DR1, UX-DR2 (shell + auth surface), UX-DR6, UX-DR8, UX-DR9 (auth/validation states), UX-DR10 (validation timing), UX-DR11, UX-DR13, UX-DR14, UX-DR15, UX-DR16 (auth-role-toggle), UX-DR17

### Epic 2: Companies post jobs; anyone can find and read them
An authenticated Company creates a Job Posting with a title and description; it becomes immediately searchable by keyword and readable in full - including the posting Company's name - by anyone, signed in or not. This epic delivers the JobPostings module end to end plus the Home / Search landing, Search results, and Job posting detail surfaces, and introduces the first cross-module Contract read (the IIdentityApi batch getter for the Company name). It stands alone on Epic 1: a Company can advertise and a Job Seeker can browse before applying exists.
**FRs covered:** FR-3, FR-4, FR-5
**NFRs / ARs carried:** NFR-4 (partial), NFR-5; AR-3 (job_postings schema), AR-4 (IIdentityApi consumer), AR-7, AR-11, AR-15, AR-19, AR-20
**UX-DRs carried:** UX-DR2 (Post-a-Job, Home/Search, Search results, Detail), UX-DR3, UX-DR5 (Publish button), UX-DR9 (catalog/search/detail states), UX-DR10 (submit-to-search, pagination), UX-DR14

### Epic 3: Job Seekers apply; Companies see who applied
An authenticated Job Seeker applies to a posting - including the signed-out apply-gate flow where an account is created and the application auto-submits against the same posting - with duplicate applications prevented; a Company views the Applicant list for a posting it owns. This closes the core loop end to end (SM-1). This epic delivers the Applications module and is the deliberate pilot of the "FSD slice mirrors backend bounded context" mapping (AR-10), with a checkpoint before the pattern propagates. It carries the three flows the architecture reviewer gate flagged: apply-gate orchestration as two chained requests (AR-16), idempotent apply on the unique-violation race (AR-17), and FR-7 returning 404 (never 403) for a missing or not-owned posting (AR-18).
**FRs covered:** FR-6, FR-7
**NFRs / ARs carried:** NFR-4 (completed), NFR-5; AR-3 (applications schema), AR-4 (IJobPostingsApi consumer), AR-7, AR-10, AR-15, AR-16, AR-17, AR-18
**UX-DRs carried:** UX-DR4, UX-DR5 (Apply button 3 states), UX-DR7 (apply-gate modal), UX-DR9 (apply/applicants/my-applications states + permission-denied), UX-DR11 (modal focus trap), UX-DR12 (both key flows + failure paths)

**Dependency flow:** Epic 2 builds on Epic 1; Epic 3 builds on Epic 1 and Epic 2. No epic requires a later epic to function.

## Epic 1: Accounts and the running skeleton

A Company and a Job Seeker can each register and sign in. The application is deployed and reachable, served same-origin, with module boundaries enforced by CI from the first commit. Delivers FR-1 and FR-2 while standing up the modular-monolith scaffold, the Identity module, cookie authentication, the architecture-test build gate, the OpenAPI 3.0 -> NSwag client pipeline, the operability floor, and the from-scratch design system with the role-aware application shell.

### Story 1.1: Walking skeleton with a CI-enforced module boundary gate

As the builder,
I want the modular-monolith solution to compile, run, and serve a health check, with CI failing the build on any module-boundary violation,
So that every later story is built on a structure whose central architectural claim (SM-2) is proven and continuously enforced.

**Acceptance Criteria:**

**Given** a clean checkout
**When** the backend solution is built
**Then** it contains `NexusJob.Host`, `NexusJob.Modules.Identity`, `NexusJob.Modules.JobPostings`, `NexusJob.Modules.Applications`, a `.Contracts` project for each module, and `NexusJob.ArchitectureTests`, and it compiles with no errors
**And** `NexusJob.Host` is the only project referencing a module implementation project; each module implementation references only other modules' `.Contracts` projects

**Given** the frontend workspace
**When** it is installed and built
**Then** it is a Vite + React 19 + TypeScript app with the FSD layer folders `app/ pages/ widgets/ features/ entities/ shared/` present
**And** `eslint-plugin-boundaries` is configured to encode the layer order `app -> pages -> widgets -> features -> entities -> shared`

**Given** the running Host and a reachable PostgreSQL 18 database
**When** `GET /health` is called
**Then** it returns `200` with a body indicating process liveness and a successful `SELECT 1` probe

**Given** the architecture test suite
**When** a module implementation is made to reference another module's non-Contracts assembly, or any project other than the Host references a module implementation, or a `.Contracts` project references an implementation, or a `FromSqlRaw` / `ExecuteSql*` call names a schema outside the caller's own
**Then** `dotnet test` fails with a message identifying the violated rule

**Given** the frontend
**When** a module in a lower FSD layer imports from a higher layer
**Then** the lint step exits non-zero

**Given** the GitHub Actions workflow
**When** it runs on a push
**Then** it builds the backend, runs the architecture tests, runs the frontend lint, and builds the container image, and it fails if any of these fail

**Given** `docker compose up`
**When** the stack is running
**Then** the Host serves the built React SPA as static files and the `/api/*` routes from a single origin, with no CORS configuration present

**Given** any request
**When** the Host writes a log line
**Then** it is structured JSON on stdout, and no credential, password hash, or cookie value appears in any log line
**And** all configuration and secrets are read from environment / .NET configuration, with nothing secret committed to the repo

### Story 1.2: From-scratch design system and the role-aware application shell

As a visitor,
I want a branded, consistently styled application shell with navigation appropriate to my signed-in state,
So that every screen that follows is built on one visual system rather than ad-hoc styling.

**Acceptance Criteria:**

**Given** the frontend `shared/` layer
**When** the design tokens are implemented
**Then** they express the DESIGN.md Nexus Indigo system exactly: the colour tokens (background `#F5F6FB`, surface `#FFFFFF`, primary `#23215E`, accent `#0A7A90`, text-primary `#1A1B2E`, text-secondary `#5B5E78`, border `#DFE1F0`, success `#0B7A42` + subtle, danger `#C42744` + subtle), the Inter type ramp (display 40/700, heading 28/700, heading-sm 20/600, body 16/400, body-sm 14/400, label 13/600 +0.04em, caption 12/500), the rounding scale (6 / 10 / 16 / 24 / full), and the spacing scale (4px base 1-8, gutter 32, editorial-gap 96)
**And** no component defines a colour, font size, radius, or spacing value outside these tokens

**Given** any surface
**When** it renders
**Then** its content sits in a fixed 1120px desktop-first max-width container with no responsive breakpoints, in light mode only

**Given** a signed-out visitor
**When** the shell renders
**Then** the navigation bar shows a "Sign up / Log in" affordance and no navigation item that points at a Company-only or Job-Seeker-only surface

**Given** the `auth-role-toggle` component
**When** it is rendered
**Then** it is a two-option pill ("Company" / "Job Seeker") with exactly one option active, the active option using the primary fill and the `full` radius, operable by `Tab` + arrow keys with `Enter` / `Space` to select

**Given** any interactive element
**When** it receives keyboard focus
**Then** a visible focus indicator is shown
**And** when `prefers-reduced-motion` is set, all fade / slide state-change transitions are suppressed

**Given** the Home / Search landing route
**When** a signed-out visitor opens the root URL
**Then** the shell renders with the landing surface present (its full search behaviour is delivered in Epic 2; an empty state is acceptable here)

### Story 1.3: Company registration and sign-in

As a hiring manager,
I want to register a Company account with my email and password and sign in with them,
So that I can access the Company features of NexusJob.

**Acceptance Criteria:**

**Given** the Identity module
**When** its persistence is set up
**Then** it owns a `company_account` table in the `identity` schema with `id` (`Guid` v7, primary key), `email`, `password_hash`, and `display_name`, plus a unique constraint on `email` within that table, and it has its own EF migration history and a `DbContext` scoped to the `identity` schema

**Given** an unregistered email
**When** `POST /api/auth/register` is called with `accountType = company`, a company name, an email, and a password
**Then** a `CompanyAccount` is created with the password stored only as a `PasswordHasher` (PBKDF2-HMAC-SHA256) hash with `IterationCount` >= 600000
**And** the response signs an `HttpOnly; Secure; SameSite=Lax` cookie carrying `NameIdentifier` = the new account id and `account_type = company`
**And** the whole operation is a single `SaveChanges` against the `identity` schema only

**Given** an email already registered as a Company
**When** `POST /api/auth/register` is called with `accountType = company` and that email
**Then** the request is rejected with an RFC 9457 ProblemDetails response and no second account is created

**Given** a registered Company
**When** `POST /api/auth/login` is called with the matching email and password
**Then** the response signs the same cookie shape as registration

**Given** a registered Company
**When** `POST /api/auth/login` is called with a wrong email or a wrong password
**Then** the response is a single generic failure that does not indicate which field was wrong

**Given** a Company has just registered
**When** they immediately call a Company-only endpoint with the issued cookie
**Then** it succeeds - no email verification or approval step gates account activation

**Given** a signed-in Company
**When** `GET /api/auth/me` is called
**Then** it returns the caller's account type and display name, resolved only from the `NameIdentifier` claim
**And** `POST /api/auth/logout` clears the cookie

**Given** any state-changing `/api/auth/*` request
**When** it is made without a valid antiforgery token
**Then** it is rejected, and a token is obtainable from `GET /api/auth/csrf`

**Given** the Host
**When** it starts
**Then** it emits one OpenAPI 3.0 document, and the frontend's TypeScript API client is generated from it by NSwag in CI, with no hand-written request/response types duplicating the API
**And** Data Protection keys are persisted to a `data_protection_keys` table in the `public` schema so the cookie survives a restart

**Given** the Sign up / Log in surface with the role toggle set to Company
**When** a visitor submits the form
**Then** fields validate on blur and again on submit (never per keystroke), and errors render inline below the field in the danger colour, programmatically associated with the field
**And** a duplicate-email registration shows "This email is already registered as a Company." inline under the email field with the other entered values retained
**And** a failed sign-in shows "That email and password don't match. Please try again."

**Given** a Company completes registration or sign-in
**When** the shell re-renders
**Then** the navigation shows the Company signed-in state and no Job-Seeker-only item

### Story 1.4: Job Seeker registration and sign-in

As a job seeker,
I want to register a Job Seeker account with my email and password and sign in with them,
So that I can later apply to postings.

**Acceptance Criteria:**

**Given** the Identity module
**When** its persistence is extended
**Then** it owns a `job_seeker_account` table in the `identity` schema with `id` (`Guid` v7, primary key), `email`, `password_hash`, and `full_name`, plus a unique constraint on `email` within that table, defined independently of `company_account`

**Given** an email not registered as a Job Seeker
**When** `POST /api/auth/register` is called with `accountType = job_seeker`, a full name, an email, and a password
**Then** a `JobSeekerAccount` is created with the same hashing and cookie rules as a Company, the cookie carrying `account_type = job_seeker`

**Given** an email already registered as a Job Seeker
**When** registration is attempted with `accountType = job_seeker` and that email
**Then** it is rejected with ProblemDetails and no second account is created

**Given** an email registered as a Company but not as a Job Seeker
**When** registration is attempted with `accountType = job_seeker` and that email
**Then** it succeeds - the two account spaces are independent

**Given** a registered Job Seeker
**When** `POST /api/auth/login` is called with `accountType = job_seeker` and matching or non-matching credentials
**Then** the matching case signs the cookie and the non-matching case returns the same generic error as FR-1

**Given** a Job Seeker has just registered
**When** they immediately use the issued cookie
**Then** the account is active - no email verification or approval step gates it

**Given** the Sign up / Log in surface
**When** the visitor switches the role toggle to Job Seeker
**Then** role-specific field errors are cleared, the entered email / password / name are kept, and the name field is labelled "Full name"
**And** sign-up mode shows the required name field above email; log-in mode never shows it

**Given** a Job Seeker completes registration or sign-in
**When** the shell re-renders
**Then** the navigation shows the Job Seeker signed-in state and no Company-only item

## Epic 2: Companies post jobs; anyone can find and read them

An authenticated Company creates a Job Posting with a title and description; it becomes immediately searchable by keyword and readable in full - including the posting Company's name - by anyone, signed in or not. Delivers the JobPostings module end to end plus the Home / Search landing, Search results, and Job posting detail surfaces, and introduces the first cross-module Contract read.

### Story 2.1: Create a job posting

As a hiring manager (authenticated Company),
I want to create a job posting with a title and description,
So that job seekers can find and apply to my opening.

**Acceptance Criteria:**

**Given** the JobPostings module
**When** its persistence is set up
**Then** it owns a `job_posting` table in the `job_postings` schema with `id` (`Guid` v7, primary key), `owner_company_id` (`Guid`, a plain column with no foreign key to any other schema), `title`, `description`, and `created_at` (`timestamptz`), with its own `DbContext` scoped to `job_postings` and its own EF migration history

**Given** an authenticated Company with a valid antiforgery token
**When** `POST /api/job-postings` is called with a non-empty title and a non-empty description
**Then** a `JobPosting` is created in a single `SaveChanges` against the `job_postings` schema only, with `owner_company_id` set from the caller's `NameIdentifier` claim and `created_at` set to the current UTC instant
**And** the response is the created posting's representation (id, title, description, createdAt) with no envelope

**Given** a request with an empty or whitespace-only title or description
**When** `POST /api/job-postings` is called
**Then** it is rejected with an RFC 9457 ProblemDetails response and no posting is created

**Given** an unauthenticated caller, or a caller whose session is a Job Seeker
**When** `POST /api/job-postings` is called
**Then** it is rejected as unauthorised and no posting is created

**Given** an authenticated Company
**When** the shell renders
**Then** the navigation shows a "Post a Job" item, and opening it shows the Post-a-Job surface with a title field, a description field, and a single primary "Publish" action rendered as the `apply-button` component in the accent colour

**Given** the Post-a-Job form
**When** the Company submits it with valid values
**Then** the confirmation "Your job posting has been published." is shown

**Given** the Post-a-Job form
**When** the submit request fails on the network or server
**Then** the entered title and description are retained, an inline banner "We couldn't publish this posting. Please try again." is shown, and a retry re-submits the same title and description

**Given** the Post-a-Job form
**When** a field is left empty
**Then** the error validates on blur and on submit (not per keystroke) and renders inline below the field in the danger colour, associated with the field for assistive tech

### Story 2.2: View a job posting's detail

As a job seeker (signed in or not),
I want to open a posting from a link and read its full title, description, and the name of the company that posted it,
So that I can decide whether to apply.

**Acceptance Criteria:**

**Given** the Identity module
**When** the `IIdentityApi` contract is published
**Then** `Identity.Contracts` exposes `GetCompany(Guid) -> CompanySummaryDto { Guid Id; string DisplayName }` and a batch `GetCompanies(IReadOnlyCollection<Guid>) -> IReadOnlyDictionary<Guid, CompanySummaryDto>`, implemented as a synchronous in-process service registered in DI and exposing no entity type

**Given** an existing posting id
**When** `GET /api/job-postings/{id}` is called with no authentication
**Then** it returns `200` with the posting's title, description, and the posting Company's display name, where the display name is obtained by calling `IIdentityApi.GetCompany` and not by a query against the `identity` schema

**Given** a posting id that does not exist
**When** `GET /api/job-postings/{id}` is called
**Then** it returns `404` with an RFC 9457 ProblemDetails response

**Given** the Job posting detail surface
**When** it is loading
**Then** a skeleton is shown for the title / description / company block

**Given** a `404` from the detail endpoint
**When** the detail surface resolves
**Then** it shows "This posting is no longer available." with a link back to Search

**Given** a Company that has just published a posting via Story 2.1
**When** the publish succeeds
**Then** the Company is redirected to this detail view for the new posting, which is live and readable

**Given** the JobPostings module
**When** the `IJobPostingsApi` contract is published
**Then** `JobPostings.Contracts` exposes `GetPostingOwner(Guid postingId) -> Guid?` (null when no such posting) and `GetPostingSummaries(IReadOnlyCollection<Guid>) -> IReadOnlyDictionary<Guid, JobPostingSummaryDto>` for later consumers, with no other module consuming it yet

### Story 2.3: Search job postings by keyword

As a job seeker (signed in or not),
I want to search postings by a keyword and browse the matches,
So that I can find openings relevant to me.

**Acceptance Criteria:**

**Given** postings exist
**When** `GET /api/job-postings?query={keyword}&page={n}&pageSize={s}` is called with no authentication
**Then** it returns a `Page<JobPostingSummaryDto> { items, page, pageSize, total }` containing the postings whose title or description contains the keyword as a case-insensitive substring, matched by an `ILIKE` / EF `Contains` query executed in the `job_postings` schema
**And** `page` is 1-based, `pageSize` defaults to 20 and is capped at 100
**And** no filtering by location, salary, or any other facet is available

**Given** a results page with several postings
**When** the response is assembled
**Then** each row's Company display name is resolved via a single batched `IIdentityApi.GetCompanies` call for all owner ids on the page, never one `GetCompany` call per row

**Given** the Home / Search landing
**When** a visitor enters a keyword and activates the explicit Search action
**Then** results refresh only on submit (not as they type), and the results render as a single-column stack of `job-card` components at gutter spacing, not a table

**Given** a `job-card` in the results
**When** the visitor clicks anywhere on it
**Then** the Job posting detail view for that posting opens
**And** hovering the card applies the DESIGN.md hover elevation as the only hover affordance
**And** the card carries no Apply control in this epic - Apply is delivered on the detail surface in Epic 3

**Given** the results list
**When** more matches exist than fit one page
**Then** pagination controls are shown (not infinite scroll), and moving pages issues a new request with the next `page` value

**Given** the landing or results surface
**When** it is loading
**Then** skeleton `job-card` rows are shown

**Given** a search that matches nothing
**When** results resolve
**Then** "No postings match \"{keyword}.\" Try a different term." is shown, with no result suggestions

**Given** no postings exist at all
**When** the landing renders its browse listing
**Then** "No open postings yet. Check back soon." is shown with no error styling

**Given** the search request fails on the network or server
**When** results resolve
**Then** "We couldn't run that search. Please try again." is shown, and a retry re-submits the same keyword

## Epic 3: Job Seekers apply; Companies see who applied

An authenticated Job Seeker applies to a posting - including the signed-out apply-gate flow where an account is created and the application auto-submits against the same posting - with duplicate applications prevented; a Company views the Applicant list for a posting it owns. This closes the core loop end to end (SM-1) and is the deliberate pilot of the "FSD slice mirrors backend bounded context" mapping (AR-10).

### Story 3.1: Apply to a posting

As an authenticated Job Seeker,
I want to apply to a job posting I'm viewing,
So that the company knows I'm interested.

**Acceptance Criteria:**

**Given** the Applications module
**When** its persistence is set up
**Then** it owns an `application` table in the `applications` schema with `id` (`Guid` v7, primary key), `job_posting_id` (`Guid`, a plain column with no cross-schema foreign key), `job_seeker_id` (`Guid`, a plain column with no cross-schema foreign key), `submitted_at` (`timestamptz`), and a unique constraint on `(job_posting_id, job_seeker_id)` defined here and in no other module, with its own `DbContext` scoped to `applications` and its own migration history

**Given** an authenticated Job Seeker with a valid antiforgery token, viewing a posting they have not applied to
**When** `POST /api/applications` is called with body `{ jobPostingId }`
**Then** the handler confirms the posting exists via `IJobPostingsApi.GetPostingOwner` (a `null` result yields `404` ProblemDetails), then creates an `Application` with `job_seeker_id` from the caller's `NameIdentifier` claim and `submitted_at` set to the current UTC instant, in a single `SaveChanges` against the `applications` schema only
**And** the response is the created application's representation

**Given** an authenticated Job Seeker who has already applied to that posting
**When** `POST /api/applications` is called again for the same posting, including a concurrent duplicate request
**Then** the insert hits the `(job_posting_id, job_seeker_id)` unique violation (`23505`), which is caught and answered with `200` and the existing application - never `409`, never `500`
**And** no pre-check `SELECT` is used as the guard

**Given** an unauthenticated caller or a Company session
**When** `POST /api/applications` is called
**Then** it is rejected as unauthorised and no application is created

**Given** an authenticated Job Seeker
**When** `GET /api/applications/mine?jobPostingId={id}` is called
**Then** it returns `{ applied: true, appliedAt }` if they have applied to that posting, otherwise `{ applied: false }`, and this endpoint lives in the Applications module - JobPostings never calls Applications to obtain it

**Given** the Job posting detail surface viewed by a signed-in Job Seeker who has not applied
**When** they click the `apply-button`
**Then** the application is submitted inline with no page navigation, and on success the button relabels to a disabled "Applied" state and "Your application has been submitted." is shown

**Given** the Job posting detail surface viewed by a signed-in Job Seeker who has already applied
**When** the surface loads
**Then** the `apply-button` renders directly in the disabled "Applied" state, its value taken from `GET /api/applications/mine`

**Given** the inline apply request fails on the network or server
**When** the Job Seeker retries
**Then** "We couldn't submit your application. Please try again." is shown with a retry action, and a retry re-submits without losing the Job Seeker's place on the posting

**Given** the Applications frontend slice
**When** it is built
**Then** it is organised as a bounded-context mirror of the backend Applications module per AR-10, and a short written checkpoint note is produced assessing whether that mapping paid off before the pattern is applied to any other frontend slice

### Story 3.2: Apply-gate for signed-out visitors

As a signed-out visitor who found a posting I want,
I want to create an account and apply without leaving the posting,
So that deciding, signing up, and applying happen in one motion.

**Acceptance Criteria:**

**Given** a signed-out visitor on the Job posting detail surface
**When** they click the `apply-button`
**Then** the `apply-gate-modal` opens as an interstitial centred over the dimmed but still-visible posting detail, with no page navigation or redirect, and focus moves to the first form field
**And** the role toggle inside the modal is pre-set to Job Seeker and is effectively fixed

**Given** the open apply-gate modal
**When** the visitor presses `Escape` or clicks the scrim
**Then** the modal closes, focus returns to the `apply-button` that opened it, no application is submitted, and nothing is lost from the posting view
**And** while the modal is open, `Tab` / `Shift+Tab` cycle only through the modal's contents

**Given** the visitor completes the modal form with a full name, an email not registered as a Job Seeker, and a password
**When** they submit
**Then** the SPA issues `POST /api/auth/register` with `accountType = job_seeker`, which creates the account and signs the cookie in one response; on success the modal closes and the SPA issues `POST /api/applications` with `{ jobPostingId }` for the posting still on screen
**And** on success of the second request the posting detail updates to the applied / confirmation state ("Your application has been submitted.") with no second Apply click

**Given** the modal form is submitted with an email already registered as a Job Seeker
**When** `POST /api/auth/register` is rejected
**Then** the modal stays open and shows "This email is already registered as a Job Seeker." inline under the email field

**Given** `POST /api/auth/register` succeeds but the subsequent `POST /api/applications` fails
**When** the failure returns
**Then** the modal still closes (the account exists and the visitor is signed in), the posting detail shows "We couldn't submit your application. Please try again." with a retry action, and the retry re-issues only the application request - the visitor is not asked to re-enter or re-create credentials

**Given** the apply-gate modal
**When** it is open
**Then** focus order within it follows visual reading order and every control is operable by keyboard alone

### Story 3.3: A Job Seeker sees the postings they applied to

As an authenticated Job Seeker,
I want a list of the postings I've applied to,
So that I can keep track of my applications.

**Acceptance Criteria:**

**Given** an authenticated Job Seeker
**When** `GET /api/applications/mine?page={n}&pageSize={s}` is called
**Then** it returns a `Page<...>` of that Job Seeker's applications, each carrying the posting's title, where the title is resolved via a single batched `IJobPostingsApi.GetPostingSummaries` call for all posting ids on the page - never one call per row
**And** rows are ordered by `submitted_at`, most recent first

**Given** an authenticated Job Seeker
**When** the shell renders
**Then** the navigation shows a "My Applications" item, absent for Companies and signed-out visitors, and opening it shows the My Applications surface

**Given** the My Applications surface
**When** it is loading
**Then** skeleton rows are shown

**Given** a Job Seeker who has not applied to anything
**When** the My Applications surface resolves
**Then** it shows "You haven't applied to anything yet." with a link to Search

### Story 3.4: A Company sees who applied to its posting

As an authenticated Company,
I want to see the list of job seekers who applied to a posting I own,
So that I know who is interested in the role.

**Acceptance Criteria:**

**Given** an authenticated Company
**When** `GET /api/job-postings/mine?page={n}&pageSize={s}` is called
**Then** it returns a `Page<JobPostingSummaryDto>` of the postings whose `owner_company_id` equals the caller's `NameIdentifier` claim, ordered by `created_at` most recent first

**Given** an authenticated Company
**When** `GET /api/applications?jobPostingId={id}` is called
**Then** the Applications handler resolves the posting's owner via `IJobPostingsApi.GetPostingOwner` and compares it to the caller's `NameIdentifier` claim
**And** if the posting does not exist, or exists but is owned by another Company, the response is `404` with an RFC 9457 ProblemDetails body that is byte-for-byte identical for both cases - never `403`
**And** if the posting is owned by the caller and has no applicants, the response is `200` with an empty `Page<ApplicantDto>`

**Given** the Identity module
**When** the `IIdentityApi` contract is extended for this story
**Then** `Identity.Contracts` additionally exposes `GetJobSeeker(Guid) -> JobSeekerSummaryDto { Guid Id; string FullName; string Email }` and a batch `GetJobSeekers(IReadOnlyCollection<Guid>) -> IReadOnlyDictionary<Guid, JobSeekerSummaryDto>` - `Email` appears on this projection and on no other Contract DTO

**Given** a posting owned by the caller with applicants
**When** `GET /api/applications?jobPostingId={id}` returns
**Then** each `ApplicantDto` carries the applicant's full name, email, and application timestamp, where name and email are resolved via a single batched `IIdentityApi.GetJobSeekers` call for all `job_seeker_id`s on the page - never one call per row
**And** rows are ordered by application timestamp, most recent first

**Given** an authenticated Company
**When** the shell renders
**Then** the navigation shows a "My Postings" item, absent for Job Seekers and signed-out visitors, and opening it lists the Company's own postings, each drillable into its Applicants view

**Given** the My Postings or Applicants view
**When** it is loading
**Then** skeleton `job-card` rows are shown for postings and skeleton `applicant-row` rows for applicants

**Given** the Applicants view for a posting with no applicants
**When** it resolves
**Then** "No applicants yet." is shown - distinct copy from the "You haven't posted a job yet." state shown when the Company has no postings at all

**Given** a Company that navigates directly to another Company's posting id in the Applicants view
**When** the view resolves the `404`
**Then** it renders the same "no longer available" not-found treatment as any missing posting - never a screen that confirms the posting exists or says "blocked"

**Given** an `applicant-row`
**When** it renders
**Then** it is read-only with no click action and no per-row controls (no scoring, status, or filtering)
