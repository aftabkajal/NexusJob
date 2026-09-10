# Epic 3 Context: Job Seekers apply; Companies see who applied

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

This epic closes the core loop of NexusJob end to end: an authenticated Job Seeker applies to a posting, and the Company that owns the posting sees the list of people who applied. It adds the third and final module, Applications, with its own schema and data. It also delivers the signed-out apply-gate flow, where a visitor creates an account and the pending application auto-submits against the posting they were already reading, with no second click and no navigation. Duplicate applications are prevented by a database constraint, and the applicant list is visible only to the posting's owner. This epic is the deliberate pilot of the "frontend slice mirrors backend bounded context" mapping, applied only to the Applications capability, with a written checkpoint before the pattern spreads to any other slice.

## Stories

- Story 3.1: Apply to a posting
- Story 3.2: Apply-gate for signed-out visitors
- Story 3.3: A Job Seeker sees the postings they applied to
- Story 3.4: A Company sees who applied to its posting

## Requirements & Constraints

- At most one application per (Job Seeker, Job Posting) pair. A repeat attempt, including a concurrent duplicate, must not create a second row and must not surface an error to the user.
- Applying requires an authenticated Job Seeker session. An unauthenticated caller or a Company session is rejected. Viewing applicants requires an authenticated Company session.
- A Company can see applicants only for postings it owns. A missing posting and a posting owned by another Company return the same not-found response, byte-for-byte identical, so existence is never leaked. There is no distinct "forbidden" state anywhere in this epic.
- A Job Seeker sees confirmation that their application was submitted. The applicant list a Company sees carries each applicant's name, email, and application timestamp, ordered most recent first. No scoring, ranking, filtering, or status workflow.
- Applicant identity (name, email) and posting titles in list views are resolved through other modules' published contracts, batched once per page, never one lookup per row.
- Keep this CRUD-shaped. No extra layering, no messaging infrastructure, no orchestration beyond what these flows need.

## Technical Decisions

- Applications is its own module with an `applications` schema, its own `DbContext` (search_path scoped to that schema), and its own EF migration history. It owns an `application` table: id (Guid v7), `job_posting_id`, `job_seeker_id` (both plain Guid columns, no cross-schema foreign key), `submitted_at` (timestamptz). The `UNIQUE (job_posting_id, job_seeker_id)` constraint lives here and nowhere else.
- Every write is one HTTP request against one module's schema in a single SaveChanges. There is no combined "register and apply" endpoint.
- Idempotent apply: the handler attempts the insert and catches the Postgres unique-violation (SQLSTATE 23505), returning 200 with the existing application. Never 409, never 500. No pre-check SELECT is used as the guard; the constraint is the guard.
- Apply-gate is two requests the SPA chains: POST `/api/auth/register` (creates the Job Seeker account and signs the cookie in one response), then on success POST `/api/applications` with `{ jobPostingId }`. No server-side orchestration. If the second request fails, the account stays created and signed in, and the SPA re-issues only the second request.
- The Apply handler verifies the posting exists via `IJobPostingsApi.GetPostingOwner(id)` (null yields 404 ProblemDetails). Caller identity always comes from the `NameIdentifier` claim.
- The applicant-list endpoint (`GET /api/applications?jobPostingId=`) is an Applications endpoint. Its handler calls `IJobPostingsApi.GetPostingOwner` and compares to the caller's account id: missing or not-owned yields 404 ProblemDetails (identical for both); owned with zero applicants yields 200 with an empty page.
- The on-load apply-button state comes from `GET /api/applications/mine?jobPostingId=` (Applications, Job Seeker session), returning `{ applied, appliedAt }` or `{ applied: false }`. JobPostings never calls Applications.
- New contract surface this epic adds: `IIdentityApi` gains `GetJobSeeker` / batch `GetJobSeekers` returning name and email (email appears on this DTO and no other). Contract DTOs are explicit named types, Guid ids, non-null unless named otherwise. There is no `IApplicationsApi`; any aggregate a Company view wants comes from an Applications HTTP endpoint, not a new backend contract edge.
- Lists use the standard envelope-free page shape `{ items, page, pageSize, total }`, offset pagination, page 1-based, pageSize default 20 / max 100, params `?page=&pageSize=`. Errors are RFC 9457 ProblemDetails. Success responses return the resource directly.
- Frontend: the Applications capability is built as a bounded-context mirror slice end to end (the AR-10 pilot). Produce a short written checkpoint note assessing whether the mapping paid off before applying it elsewhere. FSD downward-imports-only still applies; the generated client is imported only from an `entities/*/api` or `features/*/api` segment; TanStack Query keys are owned by the relevant `entities/<noun>` slice. Pagination controls, not infinite scroll, on the applicants and my-applications lists.

## UX & Interaction Patterns

- apply-button has three states on the posting detail surface. Signed-out Job Seeker: click opens the apply-gate modal. Signed-in Job Seeker who has not applied: click submits inline with no navigation, then the button relabels to a disabled "Applied" state and "Your application has been submitted." is shown. Signed-in Job Seeker who already applied: the button renders directly in the disabled "Applied" state on load. It is the single primary action per surface, in the accent color; the accent color is reserved for it.
- apply-gate modal: opens as an interstitial centered over the dimmed but still-visible posting detail, no navigation or redirect. Focus moves to the first form field. The role toggle is pre-set to Job Seeker and effectively fixed. On successful account creation the modal closes and the pending application auto-submits against the same posting; the detail updates to the confirmation state with no second click. Escape or scrim click closes it, returns focus to the triggering apply-button, submits nothing, and loses no posting-view data. Focus is trapped inside the modal while open; Tab / Shift+Tab cycle only its contents; focus order follows visual reading order.
- Modal error states: duplicate email shows "This email is already registered as a Job Seeker." inline under the email field, modal stays open. If account creation succeeds but the application submit fails, the modal still closes and the posting detail shows "We couldn't submit your application. Please try again." with a retry that re-issues only the application request; the visitor is not asked to re-enter credentials.
- Inline apply failure for a signed-in Job Seeker: "We couldn't submit your application. Please try again." with a retry that does not lose the Job Seeker's place on the posting.
- applicant-row: a full-bleed read-only row with a divider between rows, no per-row card shell and no controls (no scoring, status, or filtering). Name and email in body text, timestamp right-aligned in caption text. Rows sort by application timestamp, most recent first. Applicant lists render as a simple vertical list, never a sortable-column table.
- My Applications surface (Job Seeker nav item, spine-only, no mockup): shows skeleton rows while loading; empty state "You haven't applied to anything yet." with a link to Search.
- My Postings + Applicants surface (Company nav item): skeleton job-card rows for postings, skeleton applicant-row rows for applicants. A posting with zero applicants shows "No applicants yet." — distinct copy from "You haven't posted a job yet." shown when the Company has no postings at all. A Company opening another Company's posting id here gets the same "This posting is no longer available." not-found treatment as any missing posting, never a screen that confirms the posting exists.
- Nav is role-aware: "My Applications" appears only for a signed-in Job Seeker, "My Postings" only for a signed-in Company; neither appears for signed-out visitors or the other role.
- Microcopy is formal and professional: complete sentences, terminal punctuation, no exclamation marks, no emoji. Success and error color is always paired with a text label at AA contrast. Motion is subtle fade/slide on state change only, and is suppressed under prefers-reduced-motion. Layout is a fixed 1120px desktop-first container, light mode only, no responsive breakpoints.

## Cross-Story Dependencies

- The whole epic builds on Epic 1 (Identity module, cookie auth, CSRF, design-token system, role-aware shell, OpenAPI-to-TypeScript client pipeline) and Epic 2 (JobPostings module, `IJobPostingsApi`, the posting detail surface the apply-button and apply-gate attach to).
- Story 3.1 establishes the Applications module, its schema, the `POST /api/applications` and `GET /api/applications/mine` endpoints, and the bounded-context mirror frontend slice plus its checkpoint note. Stories 3.2, 3.3, and 3.4 all build on that module.
- Story 3.2 (apply-gate) depends on Story 3.1's apply endpoint and on Epic 1's `POST /api/auth/register` returning a signed cookie in one response.
- Story 3.4 requires extending `IIdentityApi` with `GetJobSeeker` / `GetJobSeekers` (adds email to the contract surface) and consuming `IJobPostingsApi.GetPostingOwner` for the ownership check. The backend contract graph after this epic is exactly Applications to {Identity, JobPostings} and JobPostings to Identity; no other edges.
- Story 3.3 and Story 3.4 both add role-specific nav items that must integrate with the shell established in Epic 1.
