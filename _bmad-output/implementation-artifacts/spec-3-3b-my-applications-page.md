---
title: 'My Applications page'
type: 'feature'
created: '2026-09-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f509b915bfdad13a058081b74acc1f87c0052817'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 3.3a shipped `GET /api/applications/mine/list`, but nothing in the SPA calls it — a signed-in Job Seeker has no way to see which postings they've applied to, and there is no nav path to such a page at all.

**Approach:** Add a `/my-applications` route (Job-Seeker-gated, mirroring `PostAJobPage`'s Company gate) showing a paged list of the caller's applications — skeleton rows while loading, each row linking to its posting with the title and applied date, `Pagination` reuse, and the empty state "You haven't applied to anything yet." with a link to Search. Add the "My Applications" nav item for a signed-in Job Seeker (`NavBar.navItemsFor` already carries a placeholder comment for it). This closes out story **3.3** (3.3a + 3.3b).

## Boundaries & Constraints

**Always:**
- New `entities/application` additions only — `applicationsClient.getMyApplications(page, pageSize)` (a `GET`, no CSRF) and `useMyApplications(page, pageSize)` (a bare `useQuery`, no `staleTime: Infinity` — the list changes as the seeker applies to more postings, mirrors `useJobPostingSearch`). Re-type `page`/`pageSize` as `number` in the wrapper (the generated `getMyApplications`'s params are `any` — NSwag can't infer `integer` for a plain `int` query param, the same pre-existing gap already tracked for `jobPostingsClient.search`); the response's `page`/`pageSize`/`total` stay `any` on the wire type, matching that exact precedent (deferred-work.md) — this story does not go further than 2.3a already did.
- No `enabled` gate on `useMyApplications`, unlike `useMyApplication`: the query only ever mounts inside `MyApplicationsPage`, which is itself already gated to a resolved Job Seeker session before rendering the list — there is no signed-out/Company render path that would need to skip the query.
- `MyApplicationsPage` guard mirrors `PostAJobPage` exactly: `session.isPending || session.isError` → render nothing (no flash on a cold load); `session.data?.kind !== 'jobSeeker'` → `<Navigate to="/" replace />`; the endpoint's own 401/403 is the real enforcement.
- List rendering mirrors `HomePage`'s results section structure: `query.isPending` → three skeleton rows (`aria-hidden="true"`, inside a `role="status"` region with a visually-hidden loading label); `query.isError` → `role="alert"` banner + `Retry`/`Retrying…` button calling `query.refetch()`; `query.isSuccess && total === 0` → the empty state; `query.isSuccess && total > 0` → one row per item + `Pagination` (`shared/ui`, reused as-is).
- Each row is a single whole-row `<Link to={`/job-postings/${jobPostingId}`}>` (mirrors `JobPostingCard`'s whole-card-link pattern) showing the posting title and the applied date; no per-row controls (no scoring, status, or filtering — epic-3-context: "the surface has no scoring, ranking, filtering, or status workflow").
- Prescribed copy verbatim: empty state `You haven't applied to anything yet.` with a `Link` to `/` labelled to reach Search; error banner `We couldn't load your applications. Please try again.` (mirrors `HomePage`'s "We couldn't load postings." phrasing for this surface's own resource).
- `widgets/app-shell/NavBar.tsx`'s `navItemsFor` Job Seeker case gains `{ label: 'My Applications', to: '/my-applications' }` after `Search`, mirroring the Company case's `Post a Job` addition exactly.
- `npm run lint`, `npm run test:fsd-gate`, `npm run test:api-import-gate`, `npm run test:tokens-gate`, `npm test -- --run`, and `npm run build` are all green. No backend change, no OpenAPI regen (3.3a's endpoint and client already exist).

**Never:**
- No new `entities/application/ui` component (no `ApplicationCard`/`ApplicationRow`) — DESIGN.md names no such component (My Applications is explicitly "spine-only, no mockup" per epic-3-context) and there is exactly one consumer; the row markup is inline in `MyApplicationsPage`, matching this codebase's stated preference for not forcing a premature abstraction ahead of a second consumer.
- No change to `entities/application/api/applicationsClient.ts`'s existing `apply`/`getMine` methods or their call sites (`useApplyToPosting`, `ApplyGateModal`) — additive only.
- No URL-driven page state (no `?page=` query param) — mirrors `HomePage`'s in-memory `page` state, not the browser URL.
- No application status, withdraw action, or per-row controls.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Signed-in Job Seeker, has applications | `GET /my-applications` | skeleton rows, then one row per application (title + applied date, links to the posting), most-recent first, `Pagination` shown | N/A |
| Signed-in Job Seeker, no applications | `total === 0` | `You haven't applied to anything yet.` with a link to Search (`/`) | N/A |
| Load fails | the list query rejects | `role="alert"` banner `We couldn't load your applications. Please try again.` with a `Retry` button; retry re-issues the same query | N/A |
| Pagination | more applications than the page size | clicking `Next`/`Prev` (`Pagination`) updates the page and refetches; `total` stays correct across pages | N/A |
| Signed-in Company | `session.data?.kind === 'company'` | redirected to `/` — the page never renders | N/A |
| Signed-out visitor | `session.data === null` | redirected to `/` | N/A |
| Session pending / errored | `useSession()` not resolved | page renders nothing (no flash, no redirect) until the session settles | N/A |
| Nav item | signed-in Job Seeker, shell renders | "My Applications" nav item present, absent for Company/signed-out; opens `/my-applications` | N/A |

</frozen-after-approval>

## Code Map

**`entities/application` additions** (`frontend/src/entities/application/`)
- `api/applicationsClient.ts` -- MODIFY. Add `getMyApplications: (page: number, pageSize: number): Promise<PageOfMyApplicationListItemResponse> => rawClient.getMyApplications(page, pageSize)` (a `GET`, no `callWithCsrfRetry` — mirrors `getMine`/`jobPostingsClient.search`'s re-typed-params pattern exactly). Re-export `MyApplicationListItemResponse`, `PageOfMyApplicationListItemResponse` from `shared/api`.
- `model/applicationQuery.ts` -- MODIFY. `export const applicationsMineListQueryKey = (page: number, pageSize: number) => ['application', 'mine', 'list', page, pageSize] as const`. `export function useMyApplications(page: number, pageSize: number) { return useQuery({ queryKey: applicationsMineListQueryKey(page, pageSize), queryFn: () => applicationsClient.getMyApplications(page, pageSize) }) }` (no `staleTime: Infinity`, no `enabled`).
- `index.ts` -- add the new exports; `entities/index.ts` -- re-export them in the flat barrel.
- `api/applicationsClient.test.ts` -- add `getMyApplications` cases (GET, no CSRF, resolves `PageOfMyApplicationListItemResponse`) mirroring the existing `getMine` test.
- `model/applicationQuery.test.tsx` -- add `useMyApplications` cases mirroring `useJobPostingSearch`'s test shape (resolves data, surfaces a rejection via `toApiError`).

**New — `pages/my-applications/`** (mirror `pages/home/` for the list, `pages/post-a-job/` for the guard)
- `MyApplicationsPage.tsx` -- NEW. Guard: `const session = useSession(); if (session.isPending || session.isError) return null; if (session.data?.kind !== 'jobSeeker') return <Navigate to="/" replace />;` then render the list surface. `const [page, setPage] = useState(1); const PAGE_SIZE = 20; const query = useMyApplications(page, PAGE_SIZE);` Branches exactly as `HomePage`'s results section (pending → 3 skeleton rows; error → alert + retry; success+`total===0` → empty state with `<Link to="/">`; success+`total>0` → rows + `<Pagination page={page} pageSize={PAGE_SIZE} total={query.data.total} onPageChange={setPage} />`. Each row: `<Link to={`/job-postings/${item.jobPostingId}`} key={item.applicationId} className={styles.row}>` containing the title and a formatted `submittedAt` (e.g. `new Date(item.submittedAt).toLocaleDateString()`).
- `MyApplicationsPage.module.css` -- NEW. Tokens only (tokens gate): `.row` mirrors `JobPostingCard`'s card styling (surface background, border, radius, hover elevation) minus the description line; skeleton rows mirror `JobPostingCardSkeleton`'s `@keyframes skeleton-pulse` + `prefers-reduced-motion` suppression; `.empty-state`/`.error-banner`/`.retry-button` copy `HomePage.module.css`'s classes verbatim (same tokens, same shapes).
- `MyApplicationsPage.test.tsx` -- NEW. Combines `PostAJobPage.test.tsx`'s `useSession` mock + memory-router pattern (four session states: pending/errored/wrong-role/Job-Seeker) with `HomePage.test.tsx`'s query-mocking pattern (mock `applicationsClient.getMyApplications` via the `entities` barrel or its own module; pending → skeletons; error → alert + retry click re-fetches; empty → copy + link to `/`; success → rows + pagination click advances the page and refetches).
- `index.ts` -- `export { MyApplicationsPage } from './MyApplicationsPage'`.

**Nav + routing**
- `frontend/src/widgets/app-shell/NavBar.tsx` -- MODIFY. `case 'jobSeeker': return [{ label: 'Search', to: '/' }, { label: 'My Applications', to: '/my-applications' }]`.
- `frontend/src/widgets/app-shell/NavBar.test.tsx` -- MODIFY. Update `'resolves navItemsFor to Search only'` (rename + assert `['/', '/my-applications']`) and the rendered-links assertion to include the new item, mirroring the Company case's `Post a Job` assertions.
- `frontend/src/app/App.tsx` -- MODIFY. Import `MyApplicationsPage` from `../pages`; add `{ path: 'my-applications', element: <MyApplicationsPage /> }` to the `AppShell` route's `children`.
- `frontend/src/pages/index.ts` -- MODIFY. `export { MyApplicationsPage } from './my-applications'`.
- `frontend/src/app/App.test.tsx` -- add a routing case mirroring the existing `/job-postings/:id` block: navigating to `/my-applications` as a signed-in Job Seeker renders the page; as anyone else, redirects to `/`.

**Reference — do not change**
- `frontend/src/pages/home/{HomePage.tsx,HomePage.module.css}` -- the paged-list branch structure (skeleton/error/empty/success) and copy conventions being mirrored.
- `frontend/src/pages/post-a-job/PostAJobPage.tsx` -- the session-guard pattern (pending/error → null; wrong role → redirect).
- `frontend/src/entities/job-posting/ui/{JobPostingCard.tsx,JobPostingCardSkeleton.tsx}` -- the whole-card-link and `aria-hidden` skeleton patterns this story's inline row/skeleton markup mirrors.
- `frontend/src/shared/ui/Pagination.tsx` -- reused as-is, no changes.
- `frontend/src/entities/application/api/applicationsClient.ts`, `model/applicationQuery.ts` -- `apply`/`getMine`/`useMyApplication` stay untouched; this story only adds siblings.
- `_bmad-output/implementation-artifacts/deferred-work.md` -- the existing NSwag-`any`-typing entry (3.3a) this story's re-typed wrapper works around the same way `jobPostingsClient.search` already does.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/entities/application/api/applicationsClient.ts` (+ test) -- `getMyApplications(page, pageSize)`.
- [x] `frontend/src/entities/application/model/applicationQuery.ts` (+ test) -- `applicationsMineListQueryKey` + `useMyApplications`.
- [x] `frontend/src/entities/application/index.ts` and `frontend/src/entities/index.ts` -- barrel exports.
- [x] `frontend/src/pages/my-applications/{MyApplicationsPage.tsx,MyApplicationsPage.module.css}` (+ test) -- the guarded, paged list page.
- [x] `frontend/src/pages/my-applications/index.ts` and `frontend/src/pages/index.ts` -- barrels.
- [x] `frontend/src/widgets/app-shell/NavBar.tsx` (+ test updates) -- the "My Applications" nav item for a Job Seeker.
- [x] `frontend/src/app/App.tsx` (+ test updates) -- the `/my-applications` route.

**Acceptance Criteria:**
- Given a signed-in Job Seeker who has applied to postings, when they open `/my-applications`, then they see skeleton rows while loading, then one row per application (title + applied date) linking to its posting, most-recent first, with pagination when there are more than one page.
- Given a signed-in Job Seeker who has applied to nothing, when the surface resolves, then it shows `You haven't applied to anything yet.` with a link to Search.
- Given a signed-in Company, a signed-out visitor, or an unresolved session, when `/my-applications` is requested, then a Company or signed-out visitor is redirected to `/` and an unresolved session renders nothing (no flash).
- Given a signed-in Job Seeker, when the shell renders, then the nav shows "My Applications"; it is absent for a Company or signed-out visitor.
- Given `npm run lint`, the three FSD/api-import/tokens gates, `npm test -- --run`, and `npm run build`, when they run, then all pass, including the new `entities/application`, `MyApplicationsPage`, `NavBar`, and `App` routing tests.

## Implementation Notes

- The empty-state markup is a separate `<p>` (`You haven't applied to anything yet.`) followed by its own `<Link to="/">Search</Link>`, styled with a small new `.searchLink` class (mirrors `PostingDetailPage.module.css`'s `.backLink`) rather than an inline link inside the sentence — the spec names the copy and the link but not their exact DOM relationship, and this keeps `.empty-state` byte-for-byte identical to `HomePage.module.css`'s rule as required.
- The pending-state skeleton wraps three row-shell placeholders (mirroring `JobPostingCard`'s shell minus the description line, each `aria-hidden="true"`) with a visually-hidden "Loading your applications." label, per the spec's literal wording. Originally wrapped in its own `role="status"` region — removed during review (see Review Triage Log #8): nested inside the section's own `aria-live="polite"`, a second live region risks duplicate/inconsistent screen-reader announcements. The visually-hidden label now announces through the section's single live region, matching `HomePage`'s one-live-region-per-section shape.
- Row heading uses `<h2>` (an `<h1 className={styles.srOnly}>My Applications</h1>` sits above the results region) since the page has no visible headline of its own to anchor a heading hierarchy under, unlike `HomePage`.
- `shared/ui/Pagination` gained an optional `label` prop during review (Review Triage Log #1) — defaults to `"Search results pages"` so `HomePage`'s existing usage is unaffected; `MyApplicationsPage` passes `"My applications pages"`.

## Spec Change Log

## Review Triage Log

Three layers ran on the diff since `baseline_commit`: blind-hunter (10 findings), edge-case-hunter (3 findings), verification-gap (0 findings — every changed surface is covered by tests that exercise real behavior, not mocks-only or snapshot checks).

| # | Source | Location | Finding | Verdict | Evidence |
|---|---|---|---|---|---|
| 1 | blind-hunter | `shared/ui/Pagination.tsx` | Hardcoded `aria-label="Search results pages"`, reused as-is on a page that has no search — a screen-reader user hears the wrong landmark name. | medium | Verified: no override existed. Fixed with an optional `label` prop (default preserves `HomePage`'s exact string, so that usage is unaffected); `MyApplicationsPage` passes `"My applications pages"`. Regression test added to `Pagination.test.tsx`. |
| 2 | blind-hunter | `MyApplicationsPage.tsx` pagination | Clicking Next/Prev drops into `query.isPending`, unmounting rows and `Pagination` for a skeleton flash with no `placeholderData`. | false | Identical to `HomePage`'s own already-shipped, already-reviewed pagination behavior (`useJobPostingSearch` has no `placeholderData` either) — not a regression introduced by this diff. |
| 3 | blind-hunter | `sprint-status.yaml` | The `3-3-...` line's inline comment still says 3.3b is "deferred" even though this diff ships it. | false | Procedural: step-05 of this workflow updates the comment when the story reaches `review`, mirroring every prior story. Not a code defect. |
| 4 | blind-hunter | `epic-3-context.md` | Still lists a single un-split "Story 3.3"; this diff's own Intent claims it "closes out story 3.3." | low | Confirmed stale, same documentation-drift class already logged twice (3.1a/3.1b, and 3.3a's own entry). Appended one more concise `deferred-work.md` entry noting the story is now fully shipped. |
| 5 | blind-hunter | `MyApplicationsPage.tsx` `submittedAt` rendering | `new Date(item.submittedAt).toLocaleDateString()` uses the viewer's own timezone/locale with no fixed format; no test asserts exact rendered text. | false | This is the *correct* way to show "when I applied" to an end user — their own local calendar day and locale-appropriate format, not UTC. Asserting an exact formatted string would make the test locale-fragile (environment-dependent) rather than more correct. |
| 6 | blind-hunter | `MyApplicationsPage.module.css` `.catalog` | Class name reused verbatim from `HomePage`'s "postings catalog" concept, semantically orphaned here. | low | Cosmetic naming nitpick, zero functional impact. Not worth a patch cycle on its own. |
| 7 | blind-hunter | `MyApplicationsPage.module.css` naming | Mixes camelCase and kebab-case class names, forcing bracket-notation lookups for three of them. | false | Inherited verbatim from `HomePage.module.css`'s own existing (imperfect) convention; normalizing it would mean touching `HomePage.module.css` too, out of this story's scope. |
| 8 | blind-hunter | `MyApplicationsPage.tsx` loading branch | A `role="status"` region nested inside the outer `<section aria-live="polite">` — WAI-ARIA discourages nested live regions (risk of duplicate/inconsistent screen-reader announcements). | medium | Verified: this diff's own spec asked for the `role="status"` wrapper, and it does genuinely nest inside the section's live region — `HomePage`'s skeleton has no such inner role, so this wasn't previously reviewed/accepted. Fixed by removing the inner `role="status"`; the visually-hidden label now announces through the section's single live region, matching `HomePage`'s shape. Updated the pending-state test to locate the skeleton via the section's implicit `role="region"` (mirroring `HomePage.test.tsx`'s identical pattern) instead of the removed `role="status"`. |
| 9 | blind-hunter | `MyApplicationsPage.tsx` `PAGE_SIZE` | Locally redeclared rather than shared with the backend default. | false | Matches `HomePage.tsx`'s own identical established pattern (same constant, same justifying comment) — not a new gap. |
| 10 | blind-hunter | `MyApplicationsPage.tsx` pagination | No focus/scroll management after a page change. | false | Matches `HomePage`'s identical pre-existing behavior (`onPageChange={setPage}` with no `.focus()` calls) — not introduced here; would need fixing in `HomePage` too if fixed at all. |
| 11 | edge-case-hunter | `MyApplicationsPage.tsx:86-116` | `query.isSuccess` with `query.data` null (an empty 200 body) would crash reading `.total`. | false | Matches `HomePage.tsx`'s identical non-defensive pattern (`query.data.total === 0` with no null-check); the real `GetMyApplicationsListHandler` always returns a real `Page<T>` body, never an empty one. |
| 12 | edge-case-hunter | `MyApplicationsPage.tsx:86,95` | `total` non-numeric or undefined matches neither branch, rendering blank. | false | Same reasoning as #11 — matches `HomePage`'s identical pattern; the backend always returns a real `int`. |
| 13 | edge-case-hunter | `MyApplicationsPage.tsx:105` | Unparseable `submittedAt` would render "Invalid Date". | false | The backend always serialises a valid ISO-8601 `DateTimeOffset`; no precedent anywhere in this codebase defensively validates a contract-guaranteed date string before formatting it. |

**Routing** (survivors grouped by shared root cause; `false` findings rejected outright):
- **patch** — #1 (`Pagination`'s `label` prop + regression test), #8 (remove the nested `role="status"`, retarget the pending-state test at the section's implicit region). Both applied and verified: 266/266 tests pass.
- **defer** — #4 (`epic-3-context.md` split staleness, now confirmed against a fully-shipped Story 3.3). Appended to `deferred-work.md`.
- **rejected (low / false, no action)** — #2, #3 (procedural), #5, #6, #7, #9, #10, #11, #12, #13 — each logged above with its refutation or negligible-harm rationale.

## Design Notes

- **Why the row markup is inline, not a new `entities/application/ui` component.** `JobPostingCard` earned its own file because DESIGN.md names `job-card` as a reusable component with more than one consumer in view (search results, and potentially other listings). My Applications has exactly one consumer and no named DESIGN component — extracting one now would be the premature abstraction this codebase's own Design Notes have repeatedly declined elsewhere (`Page<T>`, the auth validators before a second consumer existed).
- **Why `useMyApplications` has no `enabled` gate.** Contrast with `useMyApplication` (3.1b), which lives inside `ApplyButton` — a component that renders unconditionally across every session state and must not fire a 401/403-only request while hidden. `MyApplicationsPage` never mounts its query for a non-Job-Seeker viewer at all (the guard returns before the query hook runs), so there is no analogous state to gate against.
- **The response's `page`/`pageSize`/`total` stay `any`.** `jobPostingsClient.search` set the precedent of re-typing only the request-side params and living with the pre-existing response-typing gap (deferred-work.md); going further here would be inconsistent with that accepted resolution for the identical root cause.

## Verification

**Commands:**
- `cd frontend && npm run lint` -- expected: 0 errors.
- `cd frontend && npm run test:fsd-gate && npm run test:api-import-gate && npm run test:tokens-gate` -- expected: all pass.
- `cd frontend && npm test -- --run` -- expected: all pass, including the new/updated `entities/application`, `MyApplicationsPage`, `NavBar`, and `App` suites.
- `cd frontend && npm run build` -- expected: `tsc -b` + `vite build` clean.

**Manual checks:**
- `docker compose up`; sign in as a Job Seeker who has applied to postings → "My Applications" nav item → list shows applications, most-recent first, each linking to its posting. A Job Seeker with none → empty-state copy + working Search link. A Company or signed-out visitor never sees the nav item and is redirected to `/` if they type the URL directly.
