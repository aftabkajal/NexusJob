---
title: 'Apply surface on the posting detail page'
type: 'feature'
created: '2026-09-11'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '456ccb9c95c8da8520b387d8718da3301ae12c40'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 3.1a shipped `POST /api/applications` and `GET /api/applications/mine`, but nothing in the SPA calls them — a signed-in Job Seeker viewing a posting has no way to apply, and the `ApplicationsClient` in the generated bundle is unused.

**Approach:** Add the apply surface to `pages/posting-detail` as the deliberate AR-10 bounded-context-mirror pilot: a new `entities/application` slice (configured `ApplicationsClient` + the on-load `mine` query) and a new `features/apply-to-posting` slice (the `useMutation` + the three-state `ApplyButton`). The button is the single accent-colored primary action on the surface. This is story **3.1b**; the signed-out apply-gate modal is **Story 3.2**, and a written AR-10 checkpoint note ships with this story.

## Boundaries & Constraints

**Always:**
- FSD downward-imports-only (eslint `boundaries/dependencies`): `entities/application` may NOT import `entities/session` or `entities/job-posting`; the `ApplyButton` (which needs both session state and the posting id) lives in `features/apply-to-posting`, which may import `entities/*`. The generated client is imported only inside `entities/application/api/*` (AD-16 `no-restricted-imports`); `model/`, `ui/`, and the page reach it through the slice/entities barrel.
- `entities/application/api/applicationsClient.ts` mirrors `entities/job-posting/api/jobPostingsClient.ts`: `new ApplicationsClient('', createHttp())`; the mutating `apply` goes through `callWithCsrfRetry`, the `getMine` GET is called directly; request/response types are re-exported from `shared/api`.
- The `mine` query key is owned by `entities/application/model` (`['application', 'mine', jobPostingId] as const`), `useMyApplication(jobPostingId, enabled)` is a bare `useQuery` with `staleTime: Infinity`, `enabled` gating it to a signed-in Job Seeker (the endpoint is 401/403 otherwise). Branch on `MyApplicationResponse.applied`, never on `appliedAt` presence (3.1a Design Notes: `appliedAt` is always on the wire, `null` when not applied).
- The apply `useMutation` lives in `features/apply-to-posting/model`, mirrors `useCreatePostingForm`: `mutationFn` calls `applicationsClient.apply({ jobPostingId })`; `mutation.isPending` guards a double-click; `onSuccess` invalidates the `mine` key and flips local `submitted` state; `onError` sets a `formError` string classified via `toApiError`. No navigation on success — the posting detail stays on screen.
- `ApplyButton` visual = the existing primary-action button (`CreatePostingForm.module.css .submit`): `--color-accent` fill, `--color-accent-foreground` label in `--text-label-*`, `--radius-md`, `var(--space-3) var(--space-5)` padding. The disabled "Applied" state uses the DESIGN `apply-button` disabled pair — `--color-border` background, `--color-text-secondary` label — not `opacity`. `<button type="button">`.
- Microcopy verbatim, module-level `const`, do not reword: success `Your application has been submitted.` (a `<p role="status">`), inline failure `We couldn't submit your application. Please try again.` (a `<p role="alert">`). Complete sentences, terminal punctuation, no exclamation marks, no emoji.
- After a successful inline submit, move focus to the confirmation `<p tabIndex={-1}>` (a disabled button cannot hold focus — mirrors 2.1b's confirmation-focus review fix). The success `<p>` shows only after a fresh submit (`mutation.isSuccess`), never for the on-load already-applied state.
- `PostingDetailPage`'s existing settle-focus `useEffect` (focus to `<h1>` on success) is unchanged; `ApplyButton` never grabs focus on mount.
- `npm run lint`, `npm run test:fsd-gate`, `npm run test:api-import-gate`, `npm run test:tokens-gate`, `npm test -- --run`, and `npm run build` are all green; a `npm run generate:api` re-run leaves `git status` clean (no client change — 3.1a already regenerated it).

**Never:**
- No apply-gate modal, no focus trap, no scrim, no `apply-gate-modal` tokens — Story 3.2. No `POST /api/auth/register` chaining.
- No apply affordance for a signed-out visitor in this story (Decision, 2026-09-11): the `ApplyButton` renders `null` for a signed-out or Company session exactly as for an unresolved session — no button, no `/sign-in` stopgap, no inert control. Story 3.2 adds the signed-out branch and the modal it opens.
- No backend change, no `nexus-api-client.ts` edit, no new route (the button lives inside the existing `pages/posting-detail` at `job-postings/:id`).
- No apply-button on `JobPostingCard` (the search-results card) — this story is the detail surface only.
- No `Container`, no `shared/ui` `Button`/`Alert` extraction (the codebase hand-rolls each; keep that).
- The accent color stays reserved for this one button on the surface — no second accent element.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Signed-in Job Seeker, not applied | posting loaded, `useMyApplication` → `{ applied: false }` | `ApplyButton` renders enabled, accent, label `Apply` | N/A |
| Inline apply succeeds | click `Apply` | `apply` POST fires once; on 200 the button relabels to disabled `Applied` (border/secondary tokens), `Your application has been submitted.` shows in a `<p role="status">`, focus moves there; `mine` key invalidated; no navigation | N/A |
| Double-click | second click before the POST resolves | only one `POST /api/applications`; button is disabled while `mutation.isPending` | `isPending` guard |
| Inline apply fails | POST rejects (network / 500 / 400) | `<p role="alert">` shows `We couldn't submit your application. Please try again.`; the same button re-enables as `Apply` and is the retry; scroll position and posting content unchanged | `onError` → `formError` |
| Retry after failure | click `Apply` again | a fresh `POST /api/applications`; on 200 → the success state above | N/A |
| Signed-in Job Seeker, already applied | `useMyApplication` → `{ applied: true, appliedAt }` | button renders directly in the disabled `Applied` state on load; no `<p role="status">` message | N/A |
| `mine` query pending | signed-in Job Seeker, query not settled | button renders disabled with label `Apply` (no flash of enabled→Applied, no layout shift) | N/A |
| `mine` query errors | transient failure resolving applied-state | button renders enabled `Apply`; an accidental resubmit is safe (3.1a is idempotent → 200 with the existing row → flips to `Applied`) | N/A |
| Signed-in Company | session `kind === 'company'` | no `ApplyButton` rendered; `useMyApplication` not fired | N/A |
| Session pending / errored | `useSession()` not resolved | no `ApplyButton` rendered (appears once the session resolves to a Job Seeker) | N/A |
| Posting still loading or 404 | `useJobPosting` pending / error | no `ApplyButton` (it is a child of the success `<article>` only) | N/A |
| Signed-out visitor | `useSession()` → `null` | no `ApplyButton` rendered — same as a Company / unresolved session. Story 3.2 adds the signed-out branch (button visible → opens the apply-gate modal). No interim stopgap. (Decision, 2026-09-11.) | N/A |

</frozen-after-approval>

## Code Map

**New slice — `entities/application`** (`frontend/src/entities/application/`, mirror `entities/job-posting/`)
- `api/applicationsClient.ts` -- NEW. `import { ApplicationsClient, callWithCsrfRetry, createHttp, type CreateApplicationRequest, type ApplicationResponse, type MyApplicationResponse } from '../../../shared/api'`. `const rawClient = new ApplicationsClient('', createHttp())`. Export `applicationsClient = { apply: (body: CreateApplicationRequest) => callWithCsrfRetry(() => rawClient.create(body)), getMine: (jobPostingId: string) => rawClient.getMine(jobPostingId) }`. Re-export the three types. (Note: the generated method is `create`; the wrapper names it `apply`.)
- `model/applicationQuery.ts` -- NEW. `export const applicationMineQueryKey = (jobPostingId: string) => ['application', 'mine', jobPostingId] as const`. `export function useMyApplication(jobPostingId: string, enabled: boolean) { return useQuery({ queryKey: applicationMineQueryKey(jobPostingId), queryFn: () => applicationsClient.getMine(jobPostingId), enabled, staleTime: Infinity }) }`.
- `index.ts` -- NEW. Re-export `applicationsClient`, the types, `applicationMineQueryKey`, `useMyApplication`.
- `api/applicationsClient.test.ts`, `model/applicationQuery.test.tsx` -- NEW. Mirror `jobPostingsClient.test.ts` / `jobPostingQuery.test.tsx` (mock the generated client module; `renderHook` with a no-retry `QueryClient` wrapper; assert `enabled: false` does not fire the query).
- `frontend/src/entities/index.ts` -- add the `entities/application` re-exports to the flat barrel.

**New slice — `features/apply-to-posting`** (`frontend/src/features/apply-to-posting/`, mirror `features/create-posting/`)
- `model/useApplyToPosting.ts` -- NEW. `import { useMyApplication, applicationMineQueryKey, applicationsClient, useSession } from '../../../entities'` + `useMutation`, `useQueryClient`, `toApiError`. Reads `useSession()`; `const isJobSeeker = session.data?.kind === 'jobSeeker'`; `const mine = useMyApplication(jobPostingId, isJobSeeker)`. `const [submitted, setSubmitted] = useState(false)`, `const [formError, setFormError] = useState<string>()`. `const mutation = useMutation({ mutationFn: () => applicationsClient.apply({ jobPostingId }), onSuccess: () => { setSubmitted(true); setFormError(undefined); void queryClient.invalidateQueries({ queryKey: applicationMineQueryKey(jobPostingId) }) }, onError: (e) => setFormError(APPLY_FAILED_MESSAGE) })`. `const applied = submitted || mine.data?.applied === true`. Return `{ render: 'hidden' | 'button', applied, showConfirmation: submitted, formError, isSubmitting: mutation.isPending, disabled, apply: () => { if (mutation.isPending) return; mutation.mutate() } }` — `render === 'hidden'` when `session.isPending || session.isError || !isJobSeeker`, so a signed-out or Company session renders nothing (Decision: no signed-out affordance this story); `disabled` true while `mine.isPending` or `applied` or `isSubmitting`. Copy const: `const APPLY_FAILED_MESSAGE = "We couldn't submit your application. Please try again."`, `const APPLY_SUBMITTED_MESSAGE = 'Your application has been submitted.'`.
- `ui/ApplyButton.tsx` -- NEW. `export function ApplyButton({ jobPostingId }: { jobPostingId: string })`. Calls `useApplyToPosting(jobPostingId)`; if `render === 'hidden'` return `null`. Renders `<div className={styles.wrap}>` containing: `<button type="button" className={applied ? styles.applied : styles.apply} disabled={disabled} aria-describedby={formError ? errorId : undefined} onClick={apply}>{applied ? 'Applied' : 'Apply'}</button>`; `formError && <p id={errorId} className={styles.banner} role="alert">{formError}</p>`; `showConfirmation && <p className={styles.confirmation} role="status" tabIndex={-1} ref={confirmationRef}>{APPLY_SUBMITTED_MESSAGE}</p>`. `useEffect` moves focus to `confirmationRef` when `showConfirmation` becomes true. `useId()` for `errorId`.
- `ui/ApplyButton.module.css` -- NEW. `.apply` = copy of `CreatePostingForm.module.css .submit` minus `width: 100%` / `margin-top` (accent fill, `--radius-md`, `var(--space-3) var(--space-5)`, `--text-label-*`, `cursor: pointer`). `.apply:disabled { cursor: default; opacity: 0.7 }` (pending state). `.applied` = same box, `background-color: var(--color-border)`, `color: var(--color-text-secondary)`, `cursor: default` (no `:disabled` opacity — it is styled disabled). `.banner` = copy of `.submit`'s sibling `.banner` (danger on danger-subtle, `--radius-md`, `--text-body-sm-*`). `.confirmation` = `--color-success` on `--color-success-subtle`, same box as `.banner`. `.wrap { display: flex; flex-direction: column; gap: var(--space-3); align-items: flex-start }`. Every value a `var(--token)` (tokens gate).
- `index.ts` -- NEW. `export { ApplyButton } from './ui/ApplyButton'`.
- `model/useApplyToPosting.test.ts` (or `.tsx`), `ui/ApplyButton.test.tsx` -- NEW. Mirror `useCreatePostingForm` / `CreatePostingForm` tests: mock `../../../entities` barrel (`useSession`, `applicationsClient`, `useMyApplication`) or the underlying client modules; cover every I/O-matrix row.
- `frontend/src/features/index.ts` -- add `export { ApplyButton } from './apply-to-posting'`.

**Modified — the page**
- `frontend/src/pages/posting-detail/PostingDetailPage.tsx` -- in the success `return`, add `<ApplyButton jobPostingId={posting.id} />` as the last child of `<article className={styles.card}>`, after the description `<p>`. `import { ApplyButton } from '../../features'`. No other change — the missing-param guard, pending/skeleton, 404/error branches, and the settle-focus `useEffect` are untouched.
- `frontend/src/pages/posting-detail/PostingDetailPage.test.tsx` -- add cases: the success render shows `Apply` for a signed-in Job Seeker (mock `useSession` + `useMyApplication`/`applicationsClient` via the `entities` barrel partial-mock, following `PostAJobPage.test.tsx`'s `vi.mock('../../entities', ...)` style); no `Apply` for a Company / signed-out / pending session; no `Apply` in the pending/404 branches.

**New — AR-10 checkpoint note**
- `_bmad-output/implementation-artifacts/ar-10-mirror-slice-checkpoint.md` -- NEW. A short (≈1 page) written assessment: did organising the Applications capability as an end-to-end bounded-context-mirror slice (`entities/application` + `features/apply-to-posting` mapping the backend `Applications` module) pay off? Cover what the mapping cost (an extra `entities` slice that cannot import its siblings, the `features` layer forced to host any cross-entity composition), what it bought (clear ownership of the `mine` query key and the client wrapper; no leakage of `ApplicationsClient` past `entities/application/api`), and a recommendation on whether to apply the pattern to future slices. Written before the pattern spreads (AR-10 / epic-3-context).

**Reference — do not change**
- `frontend/src/entities/job-posting/{api/jobPostingsClient.ts,model/jobPostingQuery.ts,index.ts}` -- the slice shape being mirrored (client wrapper, query-key factory, `useQuery` hook, barrel).
- `frontend/src/entities/session/model/sessionQuery.ts` -- `useSession()` → `{ isPending, isError, data: SessionViewer | null }`; `data?.kind === 'jobSeeker'` is the discriminator.
- `frontend/src/features/create-posting/{model/useCreatePostingForm.ts,ui/CreatePostingForm.tsx,ui/CreatePostingForm.module.css}` -- the `useMutation` + `isPending` double-submit guard + `formError` + primary-button (`.submit`) + `.banner` pattern.
- `frontend/src/pages/post-a-job/{PostAJobPage.tsx,PostAJobPage.test.tsx}` -- the `useSession()` gate and the `vi.mock('../../entities', () => ({ useSession: () => useSessionMock() }))` test pattern.
- `frontend/src/shared/api/{http.ts,index.ts}` -- `createHttp` / `callWithCsrfRetry` are the only plumbing the new `api` segment needs; already export `ApplicationsClient` + the three types.
- `frontend/src/shared/tokens/tokens.css` -- token names: `--color-accent`, `--color-accent-foreground`, `--color-border`, `--color-text-secondary`, `--color-success(-subtle)`, `--color-danger(-subtle)`, `--radius-md`, `--space-3/5`, `--text-label-*`, `--text-body-sm-*`.
- `frontend/eslint.config.js`, `frontend/scripts/check-*.mjs` -- the FSD / api-import / tokens gates the new slices must pass unchanged.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/entities/application/api/applicationsClient.ts` (+ `.test.ts`) -- the configured `ApplicationsClient` wrapper (`apply` via `callWithCsrfRetry`, `getMine` direct); re-export the three types.
- [x] `frontend/src/entities/application/model/applicationQuery.ts` (+ `.test.tsx`) -- `applicationMineQueryKey` + `useMyApplication(jobPostingId, enabled)`.
- [x] `frontend/src/entities/application/index.ts` and `frontend/src/entities/index.ts` -- slice barrel + flat-barrel re-exports.
- [x] `frontend/src/features/apply-to-posting/model/useApplyToPosting.ts` (+ test) -- the `useSession` read, `useMyApplication` gate, `useMutation` (invalidate `mine` on success, `formError` on error, `isPending` guard), and the derived render/disabled/applied/confirmation state.
- [x] `frontend/src/features/apply-to-posting/ui/ApplyButton.tsx` + `.module.css` (+ test) -- the three-state button, `role="alert"` failure `<p>`, `role="status"` confirmation `<p>` with focus move, hidden branch.
- [x] `frontend/src/features/apply-to-posting/index.ts` and `frontend/src/features/index.ts` -- barrels.
- [x] `frontend/src/pages/posting-detail/PostingDetailPage.tsx` (+ `.test.tsx`) -- render `<ApplyButton jobPostingId={posting.id} />` as the last child of the success `<article>`; add the session-branch test cases.
- [x] `_bmad-output/implementation-artifacts/ar-10-mirror-slice-checkpoint.md` -- the written AR-10 assessment.

**Acceptance Criteria:**
- Given a signed-in Job Seeker on a posting they have not applied to, when they click `Apply`, then exactly one `POST /api/applications` is sent, and on success the button becomes a disabled `Applied` and `Your application has been submitted.` is shown without any navigation.
- Given a signed-in Job Seeker who has already applied, when the posting detail loads, then the button renders directly as disabled `Applied` (from `GET /api/applications/mine`) with no confirmation message.
- Given the inline `POST /api/applications` fails, when it returns, then `We couldn't submit your application. Please try again.` is shown in a `role="alert"` region, the button re-enables as `Apply`, and clicking it again re-submits — the Job Seeker's place on the posting is unchanged.
- Given a signed-in Company, a signed-out visitor (no apply affordance this story — see the Boundaries decision), or an unresolved session, when the posting loads, then no `Apply` control that submits `POST /api/applications` is rendered.
- Given `npm run lint`, `npm run test:fsd-gate`, `npm run test:api-import-gate`, `npm run test:tokens-gate`, `npm test -- --run`, `npm run build`, when they run, then all pass — including the new `entities/application` and `features/apply-to-posting` tests and the updated `PostingDetailPage` tests — and `npm run generate:api` leaves `git status` clean.

## Implementation Notes

- The implementation subagent applied review patch #1 (`key={posting.id}`) and then failed with an account-level API rate limit ("session limit" reset) before applying patches #2-#4. The orchestrating session applied the remaining three directly (`isJobSeeker` unified gate + `formError` clear in `apply()` in `useApplyToPosting.ts`; the `PostingDetailPage.test.tsx` / `useApplyToPosting.test.tsx` hardening) rather than re-dispatching a subagent into the same rate limit.
- The new "clears the stale failure message as soon as a retry begins" test initially hung: `useMutation().mutate()` in TanStack Query v5 does not call `mutationFn` synchronously inside a bare `act(() => {...})` — the first attempt read the deferred `resolve` callback before the mock's `mockImplementationOnce` factory had run, so resolving it was a no-op against a stale closure. Fixed by using `await act(async () => { result.current.apply() })` + `waitFor(() => isSubmitting === true)` before reading/resolving, mirroring the existing double-click-guard test's pattern.

## Spec Change Log

## Review Triage Log

Three layers ran on the diff since `baseline_commit`: blind-hunter (20 findings), edge-case-hunter (5 findings), verification-gap (1 finding, pre-verified, disposition patch).

| # | Source | Location | Finding | Verdict | Evidence |
|---|---|---|---|---|---|
| 1 | edge-case-hunter | `useApplyToPosting.ts` `submitted` / `formError` `useState` | `jobPostingId` changing while the hook stays mounted (a direct posting→posting nav) leaks the prior posting's `submitted` (disabled `Applied` + confirmation) onto a posting the user never applied to. | medium | Verified: neither `useState` resets on a `jobPostingId` change; only `useJobPosting`/`useMyApplication` re-key. Latent today — the app has no posting→posting transition (the `<article>` links nowhere) — but Story 3.3's My-Applications list links straight to `/job-postings/:id`. Cheapest fix: `key={posting.id}` on `<ApplyButton>` in `PostingDetailPage`, remounting the surface per posting. |
| 2 | edge-case-hunter, blind-hunter (#6) | `useApplyToPosting.ts` `apply()` | `formError` is cleared only in `onSuccess`; on a retry click the stale `role="alert"` banner and its `aria-describedby` persist through the in-flight window. Deviates from the mirrored `useCreatePostingForm.handleSubmit`, which does `setFormError(undefined)` before `mutation.mutate()`. | low | Verified against `useCreatePostingForm.ts`: it clears the form error at submit; `useApplyToPosting.apply()` does not. Brief stale-message flash on retry. Fix: clear `formError` at the top of `apply()` (or `onMutate`), matching the named precedent. |
| 3 | edge-case-hunter | `useApplyToPosting.ts:50` | The `useMyApplication` `enabled` gate (`isJobSeeker`) and the `render` gate (`isPending`/`isError`/`!isJobSeeker`) diverge: with a retained stale `data.kind==='jobSeeker'` under `session.isError`, the `mine` GET fires though the button is hidden. | low | Verified: `enabled: isJobSeeker` is not also gated on `!session.isError`/`!session.isPending`. Reachable only when `useSession` errors after a prior success with a non-401 (`fetchSession` maps 401→`null`, not error) — rare. No user-visible wrong behavior (button correctly hidden); one wasted request. Fix: one `isJobSeeker` that folds in `!isPending && !isError`, used by both gates (a simplification, single source of truth). |
| 4 | edge-case-hunter | `ApplyButton.tsx:25-27` | If the session flips to hidden after a successful submit while focus is on the confirmation `<p>`, the `<p>` unmounts and focus falls to `<body>` (the 2.1b bug). | low | Verified as a code fact, but the scenario — the viewer ceasing to be a signed-in Job Seeker in the seconds after applying, while the confirmation `<p>` still holds focus — is not a path shown to be reachable in normal use. Fix would add cross-render focus-retention branching. |
| 5 | edge-case-hunter | AC1 "exactly one POST" vs `callWithCsrfRetry` | On an antiforgery-400, `callWithCsrfRetry` re-seeds and re-issues the POST once (2 POSTs), contradicting AC1's "exactly one POST". | false | Verified: the retry is app-wide CSRF plumbing (`jobPostingsClient`, `authClient` all use it) and is covered by `applicationsClient.test.ts` ("re-seeds ... retries once on an antiforgery 400"). Per 3.1a the first POST is rejected by `AntiforgeryEndpointFilter` before the handler runs — no row written — and 3.1a is idempotent regardless. AC1's "one POST" is about the UI double-click guard, which the tests verify. Fixing the wording would edit the frozen spec. |
| 6 | verification-gap | `PostingDetailPage.test.tsx` "apply surface" block | No test observes the `jobPostingId` the page actually passes to `ApplyButton`: the button assertion is document-wide (not `within(getByRole('article'))`) and there is no `toHaveBeenCalledWith` on `useMyApplication`. A broken page→feature wiring (`jobPostingId={undefined}` / wrong field / button moved outside the `<article>`) ships green. | medium | Pre-verified by the layer: `PostingDetailPage.test.tsx:21-30` mocks `useMyApplication` (ignores args); the Job Seeker case only asserts `findByRole('button', {name:'Apply'})`. Fix: add `expect(useMyApplicationMock).toHaveBeenCalledWith('jp-1', true)` and scope the button query with `within`. |
| 7 | blind-hunter | AC + Verification sections | Dangling "per Open Question 1" references after the Open Questions section was deleted (the signed-out decision was inlined). | low | Verified. Fixed in place — both references repointed to the inline signed-out decision. Non-frozen sections; no code impact. |
| 8 | blind-hunter | `ApplyButton.module.css` `.apply:disabled { opacity: 0.7 }` | Raw `opacity` literal in a tokens-gated file; spec says "every value a var(--token)". | false | Verified: `npm run test:tokens-gate` passes; the stylelint config gates hex colors and `px` in `font-size`/`border-radius`, not bare `opacity`. `.submit:disabled { opacity: 0.7 }` in `CreatePostingForm.module.css` is the exact precedent this line copies. |
| 9 | blind-hunter | spec Reference token list | `var(--space-4)` and `var(--font-family)` are used in the CSS but not enumerated in the spec's Reference token list. | low | Both tokens exist in `tokens.css`; `tsc`, tokens-gate, and build all pass. Spec-completeness nit in a non-frozen section; no defect. |
| 10 | blind-hunter | `applicationsClient.test.ts` fixtures | POST fixtures use `submittedAt` while `MyApplicationResponse` uses `appliedAt` — reads as a mismatch; vitest does not type-check. | false | Verified against `nexus-api-client.ts`: `ApplicationResponse { id, jobPostingId, submittedAt }` and `MyApplicationResponse { applied, appliedAt }` are different DTOs — both fixtures are correct. `npm run build` runs `tsc -b`, which type-checks the test files and passed. |
| 11 | blind-hunter | Boundaries "classified via `toApiError`" | The Boundaries line says `onError` classifies via `toApiError`, but the impl (and Code Map, and I/O matrix) set one static `APPLY_FAILED_MESSAGE` for every failure. | low | Verified: Code Map spells `onError: (e) => setFormError(APPLY_FAILED_MESSAGE)`; the matrix lists one message for network/500/400; there are no per-status variants. The implementation is correct per the operative (Code Map + matrix) instructions; the frozen Boundaries phrase is a drafting imprecision and cannot be edited. No code change. |
| 12 | blind-hunter | `useApplyToPosting.ts` retry window | No test covers the in-flight interim between a retry click and its resolution. | low | Folded into #2's fix — the patch adds an assertion that `formError` is cleared when the retry begins. |
| 13 | blind-hunter | `.apply` / `.applied` CSS | No `:focus-visible` / `outline` rule on the surface's primary action. | low | Matches `.submit` exactly (no explicit focus rule there either); `border: 0` does not suppress the UA `outline`. Any gap is app-wide and pre-existing, not introduced here. |
| 14 | blind-hunter | `ApplyButton.tsx` while `isSubmitting` | No `aria-busy` / spinner / "Applying…" affordance during the POST. | low | Matches `CreatePostingForm` (which does nothing beyond `disabled` during submit); the POST is fast and the button dims + disables. Spec asks for no loading affordance. |
| 15 | blind-hunter | `PostingDetailPage.test.tsx` | No test asserts `<h1>` still holds focus after `ApplyButton` renders for a Job Seeker (the "never grabs focus on mount" claim). | low | Verified: the existing focus test runs with a non-Job-Seeker session, so the button is hidden there. Folded into #6's patch — add the `<h1>` focus assertion to the Job Seeker page test. |
| 16 | blind-hunter | `useApplyToPosting.test.tsx` | No positive assertion that a Job Seeker passes `enabled=true` to `useMyApplication` (only the Company `false` case is pinned). | low | Folded into #6's patch (`toHaveBeenCalledWith('jp-1', true)`). |
| 17 | blind-hunter | matrix "mine errors → safe resubmit" row | Only the "renders enabled" half is tested; no click-through from mine-error to a 200→`Applied`. | low | The render half is directly tested; the "safe" property is 3.1a's idempotency (tested in 3.1a) plus the generic submit→`Applied` flow (tested). Covered in aggregate. |
| 18 | blind-hunter | the diff | No sprint-status / epics / story-doc update. | false | `spec-3-1b-apply-surface.md` (the story record in this repo's convention) IS in the diff; sprint-status is correctly untouched (already at `review` under the shared `3-1` key from 3.1a — step-03's sync rule stops when already past `in-progress`); this project has no separate per-story epics update. |
| 19 | blind-hunter | empty `Implementation Notes` / `Spec Change Log` / `Review Triage Log` | Bare section headers on an `in-review` spec. | false | Per the template's own rules: `Implementation Notes` stays empty at planning time (the impl agent treats the spec as read-only source), `Spec Change Log` is empty until a `bad_spec` loopback (none), and `Review Triage Log` is populated by this very step. |
| 20 | blind-hunter | `.apply` / `.applied` CSS | Padding / border / radius / four `font-*` declarations repeated verbatim instead of `composes:`. | low | Real duplication, but it matches the codebase's established CSS-module style — `CreatePostingForm.module.css` / `AuthForm.module.css` re-declare `.banner` / `.error` from tokens rather than compose. Introducing `composes:` here would be the lone deviation. |
| 21 | blind-hunter | `applicationsClient.test.ts` imports | Spec Reference does not list `resetCsrfToken` or the antiforgery-400 title string the new tests use. | low | `shared/api/index.ts` exports `resetCsrfToken`; the sibling `jobPostingsClient.test.ts` / `authClient.test.ts` use it and the same antiforgery title (3.1a's filter copy). Tests are correct; spec Reference completeness nit only. |
| 22 | blind-hunter | `useApplyToPosting.test.tsx` invalidate assertion | `toHaveBeenCalledWith({ queryKey: ['application','mine','jp-1'] })` without `toHaveBeenCalledTimes(1)`. | low | `onSuccess` currently makes exactly one `invalidateQueries` call. Folded into #6's patch — add `toHaveBeenCalledTimes(1)` so a broader/extra invalidation is caught. |
| 23 | blind-hunter | `ar-10-mirror-slice-checkpoint.md` | Frontmatter `status: 'complete'` and a settled-sounding conclusion while the parent spec is still `in-review`. | low | The checkpoint's cost/benefit assessment and recommendation are what an AR-10 checkpoint is *for* — a considered conclusion — and code review does not overturn an architectural verdict. "Complete" describes the deliverable, which is complete. |
| 24 | blind-hunter | `useApplyToPosting.ts` return | `isSubmitting` is in the `UseApplyToPosting` interface + return but `ApplyButton` never destructures it. | low | Not dead in any harmful sense: it is a spec-Code-Map-defined field, the hook's own tests use it to observe pending state, and `disabled` (which the button uses) already folds in `mutation.isPending`. |
| 25 | blind-hunter | `PostingDetailPage.test.tsx` fixture | The new block's `posting` fixture has only `id`/`title`/`description`/`companyName`. | false | That is exactly `JobPostingDetailResponse`'s shape and matches the existing tests' fixtures; `tsc -b` (in `npm run build`) passed. |
| 26 | blind-hunter | `PostingDetailPage.test.tsx` | No page-level `session.isError` hidden-button case (pending is covered). | low | `isError → hidden` is covered at the hook and component levels; the page test's job is the wiring, which is covered for the main cases. |

**Routing** (survivors grouped by shared root cause; `false` findings rejected outright):
- **patch** — #1 (`key={posting.id}` on `<ApplyButton>` in `PostingDetailPage` to stop stale `submitted`/`formError` leaking across a posting change), #2/#12 (clear `formError` at the start of `apply()`, matching `useCreatePostingForm`; assert it in a test), #3 (fold the pending/error checks into one `isJobSeeker` used by both the `enabled` and `render` gates), #6/#15/#16/#22 (`PostingDetailPage.test.tsx` + `useApplyToPosting.test.tsx` hardening: assert `useMyApplication` is called with `posting.id` + `true`, scope the button assertion `within` the `<article>`, assert `<h1>` keeps focus after the button renders, pin the invalidate call count). Each fix is trivial, self-contained, adds no public surface.
- **rejected (low / false, no action)** — #4, #5, #7 (fixed in place), #8, #9, #10, #11, #13, #14, #17, #18, #19, #20, #21, #23, #24, #25, #26 — each logged above with its refutation or negligible-harm rationale.

## Design Notes

- **Why a `features/` slice for the button, not `entities/application/ui`.** The button's render depends on `useSession()` (an `entities/session` concern) and the posting id (an `entities/job-posting` concern). FSD forbids an `entities` slice importing a sibling `entities` slice, so a component that composes both must sit in `features/` (or higher). This is the AR-10 tension the checkpoint note is meant to record: the mirror mapping pushes all cross-entity composition up into `features/`.
- **`applied` is `submitted || mine.data?.applied === true`.** After a successful submit the `mine` query is invalidated and will refetch `applied: true`, but the local `submitted` flag flips the UI synchronously so there is no window where the button shows `Apply` again between the 200 and the refetch. Branch on `.applied`, never on `appliedAt` (3.1a serialises `appliedAt: null` always).
- **Disabled `Applied` is a styled state, not `opacity`.** DESIGN's `apply-button.disabled-background` / `disabled-foreground` are `--color-border` / `--color-text-secondary`; use those on `.applied`. The `.apply:disabled` `opacity: 0.7` is only the transient pending look while `isSubmitting`.
- **Confirmation focus.** A native `disabled` button drops focus to `<body>`. On the `submitted` transition, move focus to the `<p role="status" tabIndex={-1}>` so a keyboard user is not stranded — the same fix 2.1b's review applied to its confirmation heading.
- **No client regen.** 3.1a already ran `npm run generate:api`; `ApplicationsClient` + `CreateApplicationRequest` / `ApplicationResponse` / `MyApplicationResponse` are in `nexus-api-client.ts`. A re-run must be drift-clean.

## Verification

**Commands:**
- `cd frontend && npm run lint` -- expected: 0 errors (the new slices satisfy the FSD boundary + `no-restricted-imports` rules).
- `cd frontend && npm run test:fsd-gate && npm run test:api-import-gate && npm run test:tokens-gate` -- expected: all pass.
- `cd frontend && npm test -- --run` -- expected: all pass, including the new `entities/application` + `features/apply-to-posting` suites and the updated `PostingDetailPage` tests.
- `cd frontend && npm run build` -- expected: `tsc -b` + `vite build` clean.
- `cd frontend && npm run generate:api && git status --porcelain src/shared/api` -- expected: empty (no client change).

**Manual checks:**
- `docker compose up`; sign in as a Job Seeker, open a posting → `Apply` (accent). Click → button becomes `Applied` (grey), `Your application has been submitted.` appears, URL unchanged. Reload → button is `Applied` on load, no message.
- Sign in as a Company, open a posting → no `Apply` button. Sign out, open a posting → no `Apply` button either (Story 3.2 adds the signed-out branch).
- With DevTools throttled to fail `POST /api/applications` once → `We couldn't submit your application. Please try again.`, button back to `Apply`, retry succeeds.
