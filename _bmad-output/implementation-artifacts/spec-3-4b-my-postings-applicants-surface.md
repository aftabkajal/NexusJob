---
title: 'My Postings and Applicants Surface'
type: 'feature'
created: '2026-09-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
context: []
baseline_commit: 'b67db9293e60bb4830d86485911d20ba11165e50'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 3-4a shipped the backend (`GET /api/job-postings/mine`, `GET /api/applications?jobPostingId=`) but a Company has no UI to see its own postings or who applied to them — the last gap in Epic 3's core loop.

**Approach:** Add two Company-only pages — `/my-postings` (paged list of the Company's own postings) and `/my-postings/{id}/applicants` (paged list of that posting's applicants, DESIGN.md's `applicant-row` style) — plus a "My Postings" nav item, following the exact page/entity/routing conventions `MyApplicationsPage`/`PostAJobPage`/`PostingDetailPage` already established.

## Boundaries & Constraints

**Always:**
- Company-only session gate on both pages, mirroring `PostAJobPage` exactly: render nothing while `useSession()` is pending/erroring; `Navigate to="/"` for any resolved non-Company viewer. The endpoints' own `401`/`403` remain the real enforcement.
- `MyPostingsPage` rows: whole-row `<Link>` to `/my-postings/{id}/applicants` (no separate button), showing title + `createdAt`, mirroring `MyApplicationsPage`'s row/skeleton/error-banner/`Pagination` structure exactly. Empty state: "You haven't posted a job yet." with a link to `/post-a-job` (mirrors the Search link in `MyApplicationsPage`'s empty state).
- `ApplicantsPage` reads `:id` via `useParams`; a missing param redirects to `/my-postings` (mirrors `PostingDetailPage`'s missing-`id` guard). A persistent "Back to My Postings" link renders above the list in every state.
- `ApplicantsPage`'s `404` (missing OR not-owned posting — byte-identical per 3.4a) renders exactly "This posting is no longer available." — the same copy and `toApiError`-based branch `PostingDetailPage` uses — and nothing else about the posting. Empty state (owned, zero applicants): "No applicants yet." — distinct copy, per epics.md AC.
- `applicant-row` per DESIGN.md `{components.applicant-row}`: one bordered list container, `{colors.border}` divider between rows, no per-row card shell, no click action, no controls. Name in body text/`text-primary`, email in body-sm/`text-secondary`, timestamp right-aligned caption/`text-secondary`.
- New query hooks/keys live in each item's existing entity slice (`entities/job-posting/model`, `entities/application/model`) per the AR-10 checkpoint's 3.4 guidance — no cross-entity resolution needed since 3-4a's endpoints already batch name/email server-side. Reuse `Pagination` (with a distinct `label`) and `toApiError` on both pages.
- `navItemsFor('company')` gains `{ label: 'My Postings', to: '/my-postings' }` after "Post a Job".

**Never:**
- No backend changes — 3-4a is already merged.
- No applicant count or "View Applicants" button on `MyPostingsPage` rows: `JobPostingMineItemResponse` carries no such field, and no epics.md AC requires one — the UX mockup's richer meta line is illustrative only (DESIGN.md: the spine wins on conflict with a mock).
- No fetch of the posting's own detail (`useJobPosting`) from `ApplicantsPage` — that endpoint is public with no ownership check, so calling it would leak a not-owned posting's title/existence alongside the 404 the applicants endpoint correctly returns.
- No new `features/` slice — both pages compose `entities/session` + one `entities/*` query directly at the page level, mirroring `PostAJobPage`/`MyApplicationsPage` (unlike `ApplyButton`, no cross-entity composition is needed here).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| My Postings, has some | Company with 2+ postings | rows render title + createdAt, most-recent first, each linking to its Applicants view | N/A |
| My Postings, none | Company with zero postings | "You haven't posted a job yet." + link to `/post-a-job` | N/A |
| My Postings, loading | query pending | skeleton `job-card`-style rows | N/A |
| My Postings, load failure | non-404 query error | error banner + retry (mirrors `MyApplicationsPage`) | N/A |
| Applicants, has some | owned posting, 2+ applicants | `applicant-row`s render name/email/timestamp, most-recent first | N/A |
| Applicants, none | owned posting, zero applicants | "No applicants yet." | N/A |
| Applicants, missing/not-owned posting | `:id` matches no posting, or another Company's | "This posting is no longer available." — identical copy for both | N/A |
| Applicants, loading | query pending | skeleton `applicant-row`s | N/A |
| Applicants, other load failure | network/500 | generic retry banner | N/A |
| Applicants, missing `:id` param | route matched with an empty `id` segment | redirect to `/my-postings`, no query fires | N/A |
| Nav item visibility | signed-in Company vs. Job Seeker vs. anonymous | "My Postings" shown only for Company | N/A |
| Pagination | >1 page of postings or applicants | `Pagination` renders, page state round-trips | N/A |

</frozen-after-approval>

## Code Map

**`entities/job-posting`** (mirror `search`'s wrapper/query exactly)
- `api/jobPostingsClient.ts` -- add `getMine: (page: number, pageSize: number): Promise<PageOfJobPostingMineItemResponse> => rawClient.getMine(page, pageSize)`; re-export `JobPostingMineItemResponse`, `PageOfJobPostingMineItemResponse`.
- `api/jobPostingsClient.test.ts` -- add a `describe('jobPostingsClient.getMine')` block mirroring `search`'s two tests (happy path + URL/params assertion).
- `model/jobPostingQuery.ts` -- add `jobPostingsMineQueryKey = (page, pageSize) => ['job-postings', 'mine', page, pageSize] as const` and `useMyJobPostings(page, pageSize)` (bare `useQuery`, no `staleTime: Infinity`, mirrors `useJobPostingSearch`).
- `model/jobPostingQuery.test.tsx` -- add a case for `useMyJobPostings`.
- `index.ts` -- export the new names/types.

**`entities/application`** (mirror `getMyApplications`'s wrapper/query exactly)
- `api/applicationsClient.ts` -- add `getApplicants: (jobPostingId: string, page: number, pageSize: number): Promise<PageOfApplicantListItemResponse> => rawClient.getApplicants(jobPostingId, page, pageSize)`; re-export `ApplicantListItemResponse`, `PageOfApplicantListItemResponse`.
- `api/applicationsClient.test.ts` -- add a `describe('applicationsClient.getApplicants')` block mirroring `getMyApplications`'s tests.
- `model/applicationQuery.ts` -- add `applicantsQueryKey = (jobPostingId, page, pageSize) => ['application', 'applicants', jobPostingId, page, pageSize] as const` and `useApplicants(jobPostingId, page, pageSize)` (bare `useQuery`, mirrors `useMyApplications`).
- `model/applicationQuery.test.tsx` -- add a case for `useApplicants`.
- `index.ts` -- export the new names.

**`entities/index.ts`** -- re-export the four new names/types (flat barrel, mirrors existing entries).

**`pages/my-postings/`** (mirror `pages/my-applications/` file-for-file)
- `MyPostingsPage.tsx` -- NEW. `MyPostingsPage()`: `useSession()` gate identical to `PostAJobPage`. `MyPostingsList()`: `useState(1)` page, `useMyJobPostings(page, PAGE_SIZE)`; pending → 3 skeleton rows; error → banner + retry; `total === 0` → "You haven't posted a job yet." + `Link` to `/post-a-job`; else → one `Link` row per item (`/my-postings/{id}/applicants`, title + formatted `createdAt`) + `Pagination` (`label="My postings pages"`).
- `MyPostingsPage.module.css` -- NEW. Copy `MyApplicationsPage.module.css` verbatim, renaming `.appliedAt`/`.skeletonAppliedAt` to a `createdAt` equivalent; same tokens/shapes.
- `MyPostingsPage.test.tsx` -- NEW. Mirror `MyApplicationsPage.test.tsx`'s coverage (session gate for each viewer kind, pending/error/empty/success/pagination rendering).

**`pages/applicants/`**
- `ApplicantsPage.tsx` -- NEW. `ApplicantsPage()`: `useParams<{ id: string }>()`; missing `id` → `Navigate to="/my-postings"`. `ApplicantsList({ id })`: Company-only `useSession()` gate identical to `PostAJobPage` (checked before the query, same pending/error/redirect shape); `useState(1)` page, `useApplicants(id, page, PAGE_SIZE)`; a persistent "Back to My Postings" `Link` to `/my-postings` above the branches; pending → 3 skeleton `applicant-row`s; error → `toApiError(query.error)?.status === 404` ? "This posting is no longer available." : generic retry banner (mirrors `PostingDetailPage`'s branch); `total === 0` → "No applicants yet."; else → one read-only row per item (name, email, right-aligned formatted `submittedAt`) inside one bordered container + `Pagination` (`label="Applicants pages"`).
- `ApplicantsPage.module.css` -- NEW. DESIGN.md `{components.applicant-row}`: one `.list` container (`--color-surface`, `--color-border` border, `--radius-lg`), each `.row` `border-bottom: 1px solid var(--color-border)` (`:last-child` none), no per-row surface/radius/hover. `.name` body/`text-primary`, `.email` body-sm/`text-secondary`, `.timestamp` caption/`text-secondary`, right-aligned. Skeleton rows mirror the same `.list`/`.row` shell with shimmer lines (copy `MyApplicationsPage.module.css`'s `@keyframes skeleton-pulse` + reduced-motion guard verbatim).
- `ApplicantsPage.test.tsx` -- NEW. Mirror `PostingDetailPage.test.tsx`'s 404-vs-other-error coverage plus `MyApplicationsPage.test.tsx`'s session-gate/pending/empty/success/pagination coverage; add the missing-`id` → redirect case.

**`pages/index.ts`** -- export `MyPostingsPage`, `ApplicantsPage`.

**`widgets/app-shell/NavBar.tsx`** -- add `{ label: 'My Postings', to: '/my-postings' }` to the `'company'` case of `navItemsFor`, after "Post a Job"; update the doc comment (drop the "remains out until its surface lands" note).
**`widgets/app-shell/NavBar.test.tsx`** -- update/add a case asserting a Company viewer sees "My Postings" linking to `/my-postings`, and that Job Seeker/anonymous viewers do not.

**`app/App.tsx`** -- import `MyPostingsPage`, `ApplicantsPage`; add `{ path: 'my-postings', element: <MyPostingsPage /> }` and `{ path: 'my-postings/:id/applicants', element: <ApplicantsPage /> }` to `routes`; update the doc comment.

**Reference — do not change**
- `pages/my-applications/MyApplicationsPage.tsx`, `.module.css` -- the list-page shape `MyPostingsPage` mirrors.
- `pages/posting-detail/PostingDetailPage.tsx` -- the `toApiError`/404-branch/missing-param-guard pattern `ApplicantsPage` mirrors.
- `shared/ui/Pagination.tsx` -- reused as-is via its `label` prop.
- `_bmad-output/implementation-artifacts/ar-10-mirror-slice-checkpoint.md` -- the "list queries live in `entities/*/model`, no new `features/` slice" guidance this spec follows.
- `_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/DESIGN.md` -- `{components.applicant-row}`'s exact token spec.

## Tasks & Acceptance

**Execution:**
- [x] `entities/job-posting/api/jobPostingsClient.ts` + `.test.ts` -- `getMine` wrapper + tests.
- [x] `entities/job-posting/model/jobPostingQuery.ts` + `.test.tsx` -- `useMyJobPostings` + key.
- [x] `entities/job-posting/index.ts` -- export new names.
- [x] `entities/application/api/applicationsClient.ts` + `.test.ts` -- `getApplicants` wrapper + tests.
- [x] `entities/application/model/applicationQuery.ts` + `.test.tsx` -- `useApplicants` + key.
- [x] `entities/application/index.ts` -- export new names.
- [x] `entities/index.ts` -- re-export all new names/types.
- [x] `pages/my-postings/{MyPostingsPage.tsx,.module.css,.test.tsx}` -- the My Postings list page.
- [x] `pages/applicants/{ApplicantsPage.tsx,.module.css,.test.tsx}` -- the Applicants drill-down page.
- [x] `pages/index.ts` -- export both new pages.
- [x] `widgets/app-shell/NavBar.tsx` + `.test.tsx` -- the "My Postings" nav item.
- [x] `app/App.tsx` -- the two new routes.
- [x] `npm run generate:api` -- confirm no drift (3-4a's client is already committed; this story adds no backend/OpenAPI changes).

**Acceptance Criteria:**
- Given a signed-in Company with postings from itself and another Company, when `/my-postings` renders, then only its own postings are listed, most-recent first, each drillable into `/my-postings/{id}/applicants`.
- Given a signed-in Company viewing a posting it owns with applicants from two different Job Seekers, when `/my-postings/{id}/applicants` renders, then each row shows the correct name/email/timestamp, most-recent first, read-only with no per-row controls.
- Given a signed-in Company navigating to another Company's posting id, when `/my-postings/{other-id}/applicants` resolves the `404`, then it renders "This posting is no longer available." and nothing else about that posting.
- Given a signed-in Job Seeker or a signed-out visitor, when the shell renders, then no "My Postings" nav item appears and `/my-postings` / `/my-postings/{id}/applicants` redirect to `/`.
- Given `npm run lint`, `test:fsd-gate`, `test:api-import-gate`, `test:tokens-gate`, `npx vitest run`, and `npm run build`, when they run, then all pass with no new violations.

## Implementation Notes

Implemented as specified. `ApplicantsPage` splits into three functions
(`ApplicantsPage` for the `:id` guard, `ApplicantsList` for the Company-only
session gate, `ApplicantsForPosting` for the paged query + render) rather than
two, because React's rules of hooks forbid `useState`/`useApplicants` being
called conditionally after the session-gate early returns inside one
component — the same reason `MyApplicationsPage`/`MyApplicationsList` are
already split in two. Behaviorally this matches the spec's description
exactly (gate checked before the query, in the same conceptual unit).

Pre-existing tests that hard-coded the prior Company nav item set
(`widgets/app-shell/NavBar.test.tsx`, `app/App.test.tsx`) were updated to
expect "My Postings" alongside "Post a Job"; `app/App.test.tsx` also gained
`/my-postings` and `/my-postings/:id/applicants` route-guard coverage
(redirect for Job Seeker / anonymous, list render for Company, 404 copy for a
not-owned posting) since it already carried equivalent coverage for every
other guarded route and would otherwise be the one place these routes went
untested end-to-end.

Independently re-verified after the implementation subagent's report (step 03):
- Read `ApplicantsPage.tsx`, `MyPostingsPage.tsx`, both `.module.css` files, the entity-layer diffs (`jobPostingsClient.ts`/`.model`, `applicationsClient.ts`/`.model`, all four `index.ts` barrels), `NavBar.tsx`, and `App.tsx` directly against the diff since baseline — all match the frozen Code Map.
- `npm run lint`: 0 problems. `test:fsd-gate` / `test:api-import-gate` / `test:tokens-gate`: all three pass. `npx vitest run`: 304/304 across 30 files, matching the subagent's report. `npm run build`: clean `tsc -b && vite build`. `npm run generate:api` re-run: empty diff against the committed client — confirms no backend/OpenAPI drift, as expected for a frontend-only story.
- Matrix Test Audit: all 12 I/O-matrix rows map to a passing, correctly-named test — My Postings has-some/none/loading/load-failure in `MyPostingsPage.test.tsx`; Applicants has-some/none/missing-or-not-owned-404/loading/other-failure/missing-id-param in `ApplicantsPage.test.tsx`; nav-item visibility across all three viewer kinds in `NavBar.test.tsx`; pagination in both page test files.
- No corrections needed; implementation accepted as delivered.

## Spec Change Log

## Review Triage Log

Three review layers ran against the diff: blind-hunter (11 findings), edge-case-hunter (5 findings, JSON), verification-gap (clean — "No verification gaps found," every matrix row's test verified to actually fail on its regression).

| # | Finding | Verdict | Evidence |
|---|---|---|---|
| 1 | `ApplicantsForPosting`'s local `useState(1)` page isn't reset/re-keyed when the `:id` route param changes without a full unmount (blind-hunter + edge-case-hunter, same root cause) | low | Verified: React Router keeps the same component instance alive across a param-only URL change on the same route, so `page` would carry over. No in-app link currently causes this (the only route to an Applicants page goes through `/my-postings`, which does unmount), but direct URL-bar navigation between two ids could hit it. Fix (`key={id}`) is a one-line, zero-risk correction. **PATCHED.** |
| 2 | `jobPostingsClient.getMine`'s test is titled "propagates a 401 / 403 unchanged" but only mocks/asserts a 403 | low | Verified by reading the test: only a 403 case exists. The wrapper itself is a pure passthrough with no status-based branching, so there is no real behavioral risk — this is a test-coverage-completeness gap, not a latent bug. Trivial to add. **PATCHED** (added an explicit 401 case, mirroring `applicationsClient.getMine`'s sibling test). |
| 3 | `applicationsClient.getApplicants`'s new test block has no 401/403 propagation test at all, unlike every sibling wrapper (`apply`, `getMine`, `getMyApplications`, `jobPostingsClient.getMine`) | low | Verified by reading the full test file: only 200 and 404 cases exist for `getApplicants`. Same "pure passthrough, no real risk" reasoning as #2, but breaks this file's own established one-test-per-status-class convention. **PATCHED** (added a 403 case). |
| 4 | `deferred-work.md` has no entry for `getMine`/`getApplicants`'s NSwag `any`-typed `page`/`pageSize` params — every prior story that hit this exact systemic gap (2.3a's `search`, 3.3a's `getMyApplications`) added its own entry | low | Verified against `deferred-work.md`: the established convention is one entry per new occurrence, not one blanket entry. This diff introduces two more occurrences with no matching entries. **PATCHED** (added one entry covering both, mirroring the existing 2.3a/3.3a entries' format and wording). |
| 5 | Page-exceeds-total-pages edge case (`total > 0`, current page's `items` empty) renders an empty bordered list / catalog instead of an empty-state message, on both new pages (edge-case-hunter) | false | Verified there is no `DELETE` endpoint anywhere in the backend (`grep -ri "MapDelete\|HttpDelete"` — no matches): postings and applications are append-only for the life of a session, so `total` can only grow, never shrink, while a Company is paging through either list. `Pagination`'s own Next button is already disabled once `page >= totalPages`, so no in-app control can request an out-of-range page. The claimed precondition cannot occur. |
| 6 | Malformed/non-ISO `item.submittedAt` / `item.createdAt` would render the literal text "Invalid Date" (edge-case-hunter, both pages) | false | Both fields are server-serialized `DateTimeOffset` values (3-4a), which .NET always serializes as valid ISO-8601 — there is no code path that could produce a malformed value from this API. The exact same unguarded `new Date(x).toLocaleDateString()` pattern already ships unmodified in `MyApplicationsPage.tsx` (reviewed and accepted in 3.3b); guarding only the two new pages would be new, unrequested, inconsistent defensive code for a precondition the backend contract already rules out. |
| 7 | Neither new page's tests assert the rendered `createdAt`/`submittedAt` text actually appears on screen | defer | Verified: true of both new test files, but also true of the already-shipped, already-reviewed `MyApplicationsPage.test.tsx`, which this story was told to mirror file-for-file. Not a gap this story introduced — it replicated an existing one exactly as instructed. Deferred as a cross-cutting test-coverage gap spanning all three list pages. |
| 8 | Neither `useMyJobPostings` nor `useApplicants` sets `placeholderData`/`keepPreviousData`, so "Next" likely re-enters the full skeleton state rather than keeping the current page visible during refetch | defer | Verified via `grep -ri "placeholderData\|keepPreviousData"` across all of `entities/`: zero matches anywhere, including `useJobPostingSearch` and `useMyApplications`. A codebase-wide, pre-existing absence across every paginated query, not something this story introduced or made worse. |
| 9 | `const PAGE_SIZE = 20` and its explanatory comment are independently duplicated in both new pages (and the NSwag-gap caveat duplicated in both new client wrappers) rather than factored into a shared constant | false | This is the codebase's established, deliberate per-file duplication style — `MyApplicationsPage.tsx` already declares its own identical local `PAGE_SIZE = 20` with the same comment shape, and `jobPostingsClient.ts`'s `search`/`applicationsClient.ts`'s `getMyApplications` already duplicate the identical NSwag-gap caveat. Not a new pattern; extracting a shared constant would be an unrequested refactor beyond this story's scope. |
| 10 | No `epics.md`/sprint-status update marking the epic or story complete | false | Out of order, not a defect: `sprint-status.yaml` and any completion bookkeeping are step-05's job, which has not run yet at this point in the workflow. |
| 11 | `MyPostingsPage.module.css`'s empty-state wrapper reuses the `.catalog` class name/comment lineage from `HomePage.module.css`, which reads oddly for "You haven't posted a job yet." | false | `MyApplicationsPage.module.css` — the file this spec's Code Map explicitly directs to "copy verbatim" — already uses the identical `.catalog` name for the identical empty-state role, unflagged and already shipped. Renaming only here would create inconsistency with the mirrored sibling the spec named as the template. Internal CSS Modules class name, never user-visible. |
| 12 | The missing-`:id` redirect guard is tested via a hand-built router with a route pattern that may not reflect whether an empty `:id` segment is actually reachable via the real app route table | false | This is the identical technique and the identical open question already present, unflagged, in `PostingDetailPage.test.tsx`'s own missing-`id` guard test (which this spec's Code Map explicitly directs `ApplicantsPage` to mirror). Not a new risk introduced by this diff. |
| 13 | No focus management (e.g., moving focus to the list) when `Pagination` changes pages, on either new list | defer | Verified: `HomePage`'s and `MyApplicationsPage`'s existing `Pagination` usages have the same absence, relying only on the section's ambient `aria-live="polite"`. Pre-existing, app-wide pattern this story mirrors exactly, not something it introduced. |

Patches applied directly (findings #1–4, all trivial/low, no re-dispatch needed): `ApplicantsPage.tsx` (`key={id}`), `jobPostingsClient.test.ts` (401 case), `applicationsClient.test.ts` (403 case for `getApplicants`), `deferred-work.md` (new entry). Full verification re-run after patching (see Implementation Notes below).

## Design Notes

- **Why `ApplicantsPage` never fetches the posting's own detail.** `GET /api/job-postings/{id}` is public and has no ownership check (2.2a); calling it from `ApplicantsPage` to show a title/header would return `200` for another Company's posting even while the applicants endpoint correctly `404`s it — a direct violation of "never a screen that confirms the posting exists." The page shows a generic (screen-reader-only) "Applicants" heading and the visible "Back to My Postings" link instead.
- **Why `MyPostingsPage` rows carry no applicant count.** The UX mockup (`mockups/key-my-postings.html`) shows "N applicants" in each card's meta line and a separate "View Applicants" button, but `JobPostingMineItemResponse` (3-4a, frozen) has no count field and no epics.md AC requires one. DESIGN.md states its written spine wins over an illustrative mock on conflict; adding a count would mean either a new N+1 per-row backend call or a new aggregate endpoint, both out of scope for a frontend-only story.
- **Why `applicant-row` gets its own CSS shape instead of reusing `MyApplicationsPage`'s `.row`.** DESIGN.md's `{components.applicant-row}` is explicitly "full-bleed... no card shell per row" inside one shared bordered container with dividers — the opposite shape from the `job-card`-style boxed/hoverable rows every other list in this app uses. Building it as its own component (not a shared primitive) matches the existing precedent of not extracting a shared `Skeleton`/row primitive across dissimilar shapes (2.3a's Design Notes).

## Verification

**Commands:**
- `cd frontend && npm run lint` -- expected: 0 problems.
- `npm run test:fsd-gate && npm run test:api-import-gate && npm run test:tokens-gate` -- expected: all three gates pass.
- `npx vitest run` -- expected: all tests pass, including every new test file.
- `npm run build` -- expected: clean `tsc -b && vite build`.
- `npm run generate:api` then `git status` -- expected: no diff (this story makes no backend/OpenAPI change).
