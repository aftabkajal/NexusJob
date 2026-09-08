---
title: 'Job posting detail surface and the publish redirect'
type: 'feature'
created: '2026-09-09'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'dc3df40f05d116f8e0445d88ae2e45aa32ce0e2e'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 2.2a shipped `GET /api/job-postings/{id}` and the generated `getById` client, but nothing in the app calls it — there is no route, page, or query for viewing a posting, and Story 2.1b's publish flow still ends on an in-place "Your job posting has been published." panel instead of the detail view Story 2.2 requires.

**Approach:** Add a `getById` wrapper and a `useJobPosting(id)` TanStack Query hook to `entities/job-posting`; a public `pages/posting-detail` screen at `/job-postings/:id` that shows a loading skeleton, then the title / company name / description, or "This posting is no longer available." with a link back to Search on `404`; and rewire `features/create-posting`'s mutation `onSuccess` to `navigate(`/job-postings/${id}`)` — removing the confirmation panel and its `published` state. This is story **2.2b**; the backend is **2.2a** (done).

## Boundaries & Constraints

**Always:**
- FSD downward imports only; `npm run lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `npm test`, `npm run build` all stay green. `npm run generate:api` leaves `git status` clean (no API-shape change — 2.2a's `getById` already exists).
- The generated `shared/api` client is reached only from an `entities/*/api` segment: the `getById` call goes in `entities/job-posting/api/jobPostingsClient.ts` (a GET — no `callWithCsrfRetry`, mirroring `authClient.me`); the `model/` hook and the page import the slice's own barrel / `shared/lib`, never `shared/api` (eslint `no-restricted-imports`).
- Server state via TanStack Query, key owned by the `entities/job-posting` slice: `jobPostingQueryKey = (id) => ['job-posting', id] as const`; `useJobPosting` mirrors `useSession` (module-scope `queryFn`, `staleTime: Infinity`; the global `retry: false` default handles fast-fail on `404`).
- Every colour / font / radius / spacing value comes from `shared/tokens` (`var(--…)` in CSS Modules) — no raw literals outside the token layer. The skeleton's shimmer is suppressed under `@media (prefers-reduced-motion: reduce)`.
- Microcopy is formal (complete sentences, terminal punctuation, no exclamation/emoji). Prescribed strings verbatim: `This posting is no longer available.` (`404`, with a link back to Search) and `We couldn't load this posting. Please try again.` (non-`404` / network error, with the same back link).
- WCAG 2.1 AA floor unchanged: the loading state is a `role="status"` region with an sr-only label and `aria-hidden` shimmer bars; visible `:focus-visible` on the back link; the posting title is the page `<h1>`; full keyboard operability.
- The detail route is a child of the layout route (renders inside `AppShell` + `Container` — the page adds no `Container`), placed before the `*` not-found child. Open to everyone — no session check, no redirect.
- On a successful publish, `features/create-posting` calls `navigate(`/job-postings/${posting.id}`)`; the entered values are still retained and the inline banner still shown on a failed publish (2.1b behaviour unchanged).

**Never:**
- No backend change, no migration, no `nswag.json` / OpenAPI / CI / Vite config change, no new dependency.
- No Apply button, apply-gate, or "already applied" state — Epic 3. No `createdAt` / posted-date on the surface — 2.2a's `JobPostingDetailResponse` does not carry it.
- No search bar, results list, `job-card`, or pagination — Story 2.3.
- No new `shared/ui` component — the skeleton is inlined in `PostingDetailPage.module.css` (Story 2.3 may extract a shared `Skeleton` when it needs skeleton rows).
- No change to `entities/session`, `entities/job-posting`'s `create` wrapper, the auth flows, `RoleToggle`, or `AppShell` / `NavBar` — browse and detail add no nav item; "Back to search" is a `<Link to="/">` on the page.
- No `published` / "Post another job" affordance survives — the redirect replaces it; `useCreatePostingForm`'s `published` state, the confirmation panel, its focus `useEffect`, and `reset()` are removed, not left orphaned.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Open a real posting | navigate to `/job-postings/{existing id}` | while `useJobPosting` is pending: a `role="status"` skeleton for the title / company / description block; then a card with `<h1>` = title, the company display name, and the description (line breaks preserved) | N/A |
| Posting not found | `/job-postings/{well-formed unknown id}` → `getById` rejects `404` | the skeleton resolves to `This posting is no longer available.` and a "Back to search" link to `/` | `toApiError(error)?.status === 404` |
| Non-`404` failure | `getById` rejects `500` / network error | `We couldn't load this posting. Please try again.` and the "Back to search" link | any error where `toApiError(error)?.status !== 404` |
| Missing route param | `/job-postings/` — not matchable by `:id`, defensive only | `<Navigate to="/" replace />` | `!id` guard |
| Publish succeeds | signed-in Company submits a valid posting; `jobPostingsClient.create` resolves `{ id, … }` | `navigate(`/job-postings/${id}`)` — the detail view for the new posting renders (live and readable); no confirmation panel | N/A |
| Publish fails, network / non-field | `create` rejects | unchanged from 2.1b: inline banner `We couldn't publish this posting. Please try again.`, entered title/description retained, retry re-submits; no navigation | `toApiError` → banner |
| Publish fails, field `400` | `create` rejects `400` with an `errors` map | unchanged from 2.1b: per-field errors under Title / Description; no navigation | `400` + `errors` → per-field |
| Anonymous / Job Seeker opens a detail URL | `/job-postings/{id}` with no session or a Job Seeker session | identical `200` detail render — the page has no session gate | N/A |

</frozen-after-approval>

## Code Map

**Entities — `job-posting` slice gains the read path**
- `frontend/src/entities/job-posting/api/jobPostingsClient.ts` -- add `type JobPostingDetailResponse` to the `shared/api` import; add `getById: (id: string): Promise<JobPostingDetailResponse> => rawClient.getById(id)` to the exported object (no `callWithCsrfRetry` — a GET, like `authClient.me`); add `JobPostingDetailResponse` to the `export type { … }` line.
- `frontend/src/entities/job-posting/model/jobPostingQuery.ts` -- NEW, mirroring `entities/session/model/sessionQuery.ts`: `export const jobPostingQueryKey = (id: string) => ['job-posting', id] as const`; `export function useJobPosting(id: string) { return useQuery({ queryKey: jobPostingQueryKey(id), queryFn: () => jobPostingsClient.getById(id), staleTime: Infinity }) }`. No local error catch — a `404` (or any failure) rejects and the page branches via `toApiError`.
- `frontend/src/entities/job-posting/index.ts` -- add `export { jobPostingQueryKey, useJobPosting } from './model/jobPostingQuery'`; add `type JobPostingDetailResponse` to the client re-export.
- `frontend/src/entities/index.ts` -- add `jobPostingQueryKey`, `useJobPosting`, `type JobPostingDetailResponse` to the explicit `./job-posting` re-export list.

**Pages — the detail screen**
- `frontend/src/pages/posting-detail/PostingDetailPage.tsx` -- NEW. `const { id } = useParams<{ id: string }>()`; `if (!id) return <Navigate to="/" replace />`; `const query = useJobPosting(id)`. `query.isPending` → a `<div role="status">` with an sr-only "Loading the job posting." and three `aria-hidden` shimmer bars (`styles.skeletonLine`). `query.isError` → `toApiError(query.error)?.status === 404` ? the "no longer available" panel : the "couldn't load" panel — each a short `<p className={styles.message}>` plus `<Link to="/" className={styles.backLink}>Back to search</Link>`. `query.isSuccess` → `<article className={styles.card}>` with `<h1>{data.title}</h1>`, `<p className={styles.company}>{data.companyName}</p>`, `<p className={styles.description}>{data.description}</p>`.
- `frontend/src/pages/posting-detail/PostingDetailPage.module.css` -- NEW, tokens only. `.card` mirrors `CreatePostingForm.module.css` `.card` (`--radius-lg` / `--color-surface` / `1px solid --color-border` / `padding: --space-6` / `gap: --space-4` / `max-width: 640px` / `margin-inline: auto`). `.company` = `--color-text-secondary`, `label` type-ramp props. `.description` = `body` type-ramp props, `white-space: pre-wrap`. `.skeletonLine` = `--color-border` block, `--radius-sm`, heights/widths from `--space-*`, a subtle opacity `@keyframes` pulse; `@media (prefers-reduced-motion: reduce) { .skeletonLine { animation: none } }`. `.backLink` = `--color-accent`, underlined, own `:focus-visible` not needed (global `base.css` covers it). `.message` = `body` type-ramp props.
- `frontend/src/pages/posting-detail/index.ts` -- NEW: `export { PostingDetailPage } from './PostingDetailPage'`.
- `frontend/src/pages/index.ts` -- add `export { PostingDetailPage } from './posting-detail'` (alphabetical).

**App — the route**
- `frontend/src/app/App.tsx` -- add `{ path: 'job-postings/:id', element: <PostingDetailPage /> }` to the layout route's `children`, before the `{ path: '*' }` child; import `PostingDetailPage` from `../pages`.

**Features — publish redirects instead of confirming**
- `frontend/src/features/create-posting/model/useCreatePostingForm.ts` -- `import { useNavigate } from 'react-router'`; `const navigate = useNavigate()` (pattern from `useAuthForm.ts`). Type the mutation `mutationFn` as `Promise<JobPostingResponse>` (import the type from `../../../entities`); `onSuccess: (posting) => navigate(`/job-postings/${posting.id}`)`. Remove `published` state / `setPublished`, the `reset()` function, and `published` / `reset` from the returned object.
- `frontend/src/features/create-posting/ui/CreatePostingForm.tsx` -- delete the `if (published) { return … }` confirmation panel, `confirmationRef`, `hasPublished`, and the `published`-transition `useEffect`. Keep `titleRef` and the form. Stop destructuring `published` / `reset` from the hook.
- `frontend/src/features/create-posting/ui/CreatePostingForm.module.css` -- remove the now-unused `.confirmation` rule.

**Tests**
- `frontend/src/entities/job-posting/api/jobPostingsClient.test.ts` -- add: `getById` issues `GET /api/job-postings/{id}` and resolves the `JobPostingDetailResponse`; a `404` rejection propagates unchanged (no CSRF retry).
- `frontend/src/entities/job-posting/model/jobPostingQuery.test.tsx` -- NEW, mirroring `sessionQuery.test.tsx` (`renderHook` + a fresh `QueryClient` wrapper, mock `jobPostingsClient.getById`): resolve → `isSuccess` + `data`; reject `{ status: 404 }` → `isError` with `toApiError(result.current.error)?.status === 404`.
- `frontend/src/pages/posting-detail/PostingDetailPage.test.tsx` -- NEW. Mount at `/job-postings/x` with `createMemoryRouter` + a mocked `jobPostingsClient.getById`: pending → `getByRole('status')`; resolved → the title heading, the company text, the description text; `404` → `This posting is no longer available.` + a link to `/`; a `500` → `We couldn't load this posting. Please try again.` + the link.
- `frontend/src/features/create-posting/ui/CreatePostingForm.test.tsx` -- rewrite the success cases: a resolved `create({ id: 'new-id', … })` drives `router.state.location.pathname` to `/job-postings/new-id`; drop the `role="status"` confirmation + "Post another job" assertions. Keep the validation-timing, failure-banner-retention + retry, and `400`-field-mapping cases.
- `frontend/src/app/App.test.tsx` -- update the `/post-a-job` success expectation (now navigates to `/job-postings/:id`); add: `/job-postings/{id}` renders the detail card for a mocked `getById`, and an unknown id shows `This posting is no longer available.`.

**Reference — do not change**
- `frontend/src/shared/api/nexus-api-client.ts` (generated — already has `getById` / `JobPostingDetailResponse` from 2.2a), `frontend/src/entities/session/**`, `frontend/src/widgets/app-shell/**`, `frontend/src/features/auth/**`, `frontend/src/app/queryClient.ts`.
- `_bmad-output/implementation-artifacts/spec-2-1b-post-a-job-frontend.md` -- continuity: the `entities/job-posting` slice + `jobPostingsClient` wrapper shape, the `useCreatePostingForm` / `CreatePostingForm` structure being edited, the `useAuthForm` `useNavigate` pattern, the `App.test.tsx` `createMemoryRouter` harness, the token set.
- `_bmad-output/implementation-artifacts/spec-2-2a-detail-endpoint-and-contracts.md` -- continuity: `GET /api/job-postings/{id}` → `{ id, title, description, companyName }`; `404` is RFC 9457 problem+json; the endpoint is anonymous.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/entities/job-posting/{api/jobPostingsClient.ts,model/jobPostingQuery.ts,index.ts}` + `frontend/src/entities/index.ts` -- the `getById` wrapper, the `useJobPosting` / `jobPostingQueryKey` hook, barrels.
- [x] `frontend/src/pages/posting-detail/{PostingDetailPage.tsx,PostingDetailPage.module.css,index.ts}` + `frontend/src/pages/index.ts` -- the page (skeleton / success / 404 / non-404-error states), token-only CSS, barrels.
- [x] `frontend/src/app/App.tsx` -- the `job-postings/:id` route before the `*` child.
- [x] `frontend/src/features/create-posting/{model/useCreatePostingForm.ts,ui/CreatePostingForm.tsx,ui/CreatePostingForm.module.css}` -- redirect on publish success; remove the `published` panel, its ref/effect, and `reset`.
- [x] `frontend/src/entities/job-posting/api/jobPostingsClient.test.ts` + `frontend/src/entities/job-posting/model/jobPostingQuery.test.tsx` -- `getById` request + resolve + `404` propagation; the hook's success and `404` branches.
- [x] `frontend/src/pages/posting-detail/PostingDetailPage.test.tsx` -- pending skeleton, success render, `404` copy + link, non-`404` copy + link.
- [x] `frontend/src/features/create-posting/ui/CreatePostingForm.test.tsx` + `frontend/src/app/App.test.tsx` -- publish-success navigation to `/job-postings/:id`; the detail route renders / unknown-id copy; drop the confirmation-panel assertions.

**Acceptance Criteria:**
- Given a signed-in Company that submits a valid posting, when `create` resolves, then the app navigates to `/job-postings/{new id}` and that detail view renders the posting; no "Your job posting has been published." panel remains anywhere in the code.
- Given `/job-postings/{id}` for an existing posting, when the page loads, then a `role="status"` skeleton shows first, then the posting title as the page `<h1>`, the company display name, and the description.
- Given `/job-postings/{id}` where the id has no posting, when `getById` returns `404`, then the surface shows "This posting is no longer available." with a link to `/`; a non-`404` failure shows "We couldn't load this posting. Please try again." with the same link.
- Given an anonymous or Job Seeker visitor opening a valid `/job-postings/{id}`, when it resolves, then the same `200` detail renders with no redirect.
- Given the CI frontend gates (`lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `npm test`, `npm run build`), when they run, then all pass and `npm run generate:api` leaves `git status` clean.

## Implementation Notes

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-09) — blind-hunter, edge-case-hunter, verification-gap

Local re-verification before the pass was green: `npm run lint` / `lint:tokens` / the three gates / 134 tests / `npm run build` / `generate:api` drift-clean. `verification-gap`: **no gaps** — every behavioral change is covered by a running test. No `intent_gap`, no `bad_spec` — no loopback. 3 `patch`, 1 `defer`, the rest rejected.

**patch:**
- `PostingDetailPage.tsx` (verification-gap "other", blind-hunter, edge-case-hunter — one root cause) — `low`. 2.1b deliberately moved keyboard focus onto the `role="status"` confirmation on publish success so the unmounting Publish button did not strand focus. The redirect removes that scenario but adds no replacement: after `navigate(`/job-postings/${id}`)` focus falls to `<body>`, and `PostingDetailPage` never takes focus on settle (also affects a direct detail-link visit). Fix: on the query settling, move focus to the landing element — `<h1>` on success, the message `<p>` on error — each `tabIndex={-1}`, mirroring 2.1b's `confirmationRef.current?.focus()`; add a focus assertion.
- `entities/job-posting/model/jobPostingQuery.ts` (blind-hunter) — `low`. The JSDoc claims `useJobPosting` "mirrors `useSession`: a module-scope `queryFn`", but `queryFn: () => jobPostingsClient.getById(id)` must close over the hook's `id` and cannot be hoisted. Fix: reword — it mirrors `useSession` in shape (`useQuery` + `staleTime: Infinity` + no local error catch), not in queryFn placement.
- `features/create-posting/ui/CreatePostingForm.tsx` (verification-gap "other") — `low`. `titleRef` (`useRef` + `ref={titleRef}` on the Title input) is now write-only — its only reader, the deleted focus `useEffect`, is gone. Fix: remove `titleRef` and the prop; drop the `useRef` import if unused.

**defer:**
- No app-wide route-change announcement (blind-hunter, edge-case-hunter) — `low`. After a client-side redirect (`SignInPage`, `PostAJobPage`, and now the publish → `/job-postings/:id` redirect) and after `PostingDetailPage`'s async content resolves, there is no `aria-live` route announcer and no `document.title` update, so a screen-reader user gets no cue that the page changed or finished loading. Pre-existing pattern; the `patch` above restores keyboard-focus continuity, but an announcement layer is its own a11y pass. → `deferred-work.md`.

**rejected:**
- Read-after-write race — publisher redirected to the detail route sees "This posting is no longer available." (blind-hunter, edge-case-hunter) — `false`. `POST /api/job-postings` commits synchronously to a single Postgres before returning `200`; the subsequent `getById` reads the same database (read-committed, no replica), so the row is visible. epics.md Story 2.2 AC asserts "which is live and readable". Seeding `queryClient.setQueryData` would only skip one request, and `create`'s response omits `companyName` so a complete seed is impossible anyway.
- Generic-failure card says "Please try again." but offers only "Back to search", no retry button (blind-hunter) — `low`. The copy is the frozen-matrix string; "try again" reasonably means reload / revisit; non-`404` failures are rare (the `500` orphan path is unreachable in v1); a `refetch` button adds surface the epic does not ask for (its AC covers only the `404` + back-link).
- `if (!id) return <Navigate to="/" replace />` is unreachable via the real route tree (blind-hunter) — `low`. `useParams<{id: string}>()` is typed `id: string | undefined`, so the guard narrows the type for `useJobPosting(id)` / `getById(id)`; `<Navigate>` is a safe fallback for the can't-happen case. Matches the 2.1a / 2.2a precedent of keeping cheap defensive guards; the alternative (`id!`) hides the assumption, and a bare `/job-postings` index route has no product need (Story 2.3 owns search at `/`).
- `getById` client test omits the generated `status === 500` branch (blind-hunter) — `low`. NSwag-generated boilerplate, identical across every client method, `eslint`-ignored, not hand-editable (same rationale the 2.2a review used); the behavioral consequence (non-`404` → the load-failed copy + link) is covered by `PostingDetailPage.test.tsx`.
- No raw-`TypeError` / empty-body-`404` error test (blind-hunter) — `low`. `toApiError`'s handling of an `ApiException`, a ProblemDetails object, and a non-error is covered by `shared/lib/toApiError.test.ts` (2.1b); a raw `fetch` `TypeError` flows to `toApiError(...) → null → ?.status !== 404 →` the generic message (verified). Adding variants re-tests `toApiError`.
- Weak line-break assertion — description test checks only the first line (blind-hunter) — `low`. `white-space: pre-wrap` is a CSS behavior jsdom does not reflow; `{posting.description}` renders the raw string verbatim, so asserting the second line re-tests React text rendering. The spec's manual-checks cover the visual line breaks.
- `navigate(`/job-postings/${posting.id}`)` not URL-encoded / `posting.id` unvalidated (blind-hunter, edge-case-hunter) — `low`. `posting.id` is a server-generated canonical GUID (`Guid.CreateVersion7().ToString()`), typed `string` on `JobPostingResponse`, never empty and with no characters needing encoding. Guards a backend-contract violation 2.2a's tests rule out.
- Unconditional render of possibly-empty `title` / `companyName` / `description` (blind-hunter, edge-case-hunter) — `low`. All three are backend-guaranteed non-empty: `CompanyAccount.DisplayName` and the posting fields are `[Required(AllowEmptyStrings = false)]` and trimmed at write time (stories 1.3a / 2.1a). An empty-field render is unreachable without a contract violation.
- "Back to search" label vs arriving from a shared link (blind-hunter) — `low`. epics.md Story 2.2 AC specifies "a link back to Search", and `/` is the Home / Search landing (epic context); "Back to" is idiomatic for a nav link regardless of the visitor's actual history.

## Design Notes

- **The detail query mirrors `useSession`.** Module-scope `queryFn`, key owned by the slice (`['job-posting', id]`), `staleTime: Infinity` (a v1 posting is immutable). The global `QueryClient` default `retry: false` means a `404` fails on the first attempt — no local `retry` needed. The page branches on `toApiError(query.error)?.status`, exactly as `fetchSession` branches on `401`.
- **Skeleton is inlined, not a shared component.** Story 2.2b needs three shimmer bars in one place; a reusable `shared/ui/Skeleton` is Story 2.3's call (skeleton *rows* for the results list). The loading `<div>` carries `role="status"` + an sr-only label so assistive tech announces the wait; the bars are `aria-hidden`; the pulse is off under `prefers-reduced-motion`.
- **The redirect deletes the `published` state machine.** 2.1b's `CreatePostingForm` had a `published` flag, a confirmation panel, a focus `useEffect`, and a `reset()` for "Post another job". With the mutation `onSuccess` navigating away, the form unmounts on success — all of that becomes dead code and is removed. The failure path (`formError` banner, value retention, retry) is untouched.
- **No session gate on the detail route.** Per AD-18 / the epic, browse and detail are open to everyone. Unlike `PostAJobPage`, `PostingDetailPage` has no `useSession` call and no `<Navigate>` beyond the defensive `!id` check.

## Verification

**Commands:**
- `cd frontend && npm ci` -- clean install (lockfile unchanged).
- `npm run lint && npm run test:fsd-gate && npm run test:api-import-gate` -- FSD + AD-16 boundaries clean (the `getById` wiring stays in `entities/job-posting/api/`).
- `npm run lint:tokens && npm run test:tokens-gate` -- no raw hex / disallowed `px` in the new `PostingDetailPage.module.css`.
- `npm test -- --run` -- all specs pass, including the new `jobPostingQuery`, `PostingDetailPage`, updated `jobPostingsClient`, `CreatePostingForm`, and `App` suites.
- `npm run build` -- `tsc -b` + `vite build` succeed.
- `npm run generate:api && git status --porcelain src/shared/api` -- empty (2.2a already generated `getById`).

**Manual checks:**
- `docker compose up`; sign in as a Company, publish a posting → the URL becomes `/job-postings/<guid>` and the page shows the title, your company name, and the description. Reload that URL → same, no session needed. Sign out and open it → still renders.
- Edit the URL to a random GUID → "This posting is no longer available." with a working "Back to search" link. Stop the Host and open a valid detail URL → "We couldn't load this posting. Please try again."
- Keyboard: Tab to "Back to search" on the 404 view — visible focus ring; OS reduced-motion → the loading bars do not pulse.
