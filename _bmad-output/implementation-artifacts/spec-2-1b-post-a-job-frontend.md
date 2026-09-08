---
title: 'Post-a-Job frontend surface'
type: 'feature'
created: '2026-09-07'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: '89f59bb12b1fa1697f6c76bbf53cae32faf3bd2e'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 2.1a shipped `POST /api/job-postings` and the generated `JobPostingsClient`, but there is no UI. A signed-in Company has no "Post a Job" nav item, no route, and no form — it cannot create a posting through the app.

**Approach:** Add a `features/create-posting` slice (a title + description form with the Publish `apply-button`), an `entities/job-posting` slice that owns the configured `JobPostingsClient`, a `pages/post-a-job` screen wired at `/post-a-job` and guarded to signed-in Companies, and a "Post a Job" nav item for a Company. Because a second `entities/*` slice now needs the credentials / CSRF `fetch` plumbing and the `toApiError` normaliser, first lift those from `entities/session/api/` into `shared/` (a behaviour-preserving refactor). This is story **2.1b**; the backend is **2.1a** (`spec-2-1a-job-postings-create-endpoint.md`, done).

## Boundaries & Constraints

**Always:**
- FSD downward imports only; `npm run lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `npm test`, `npm run build` all stay green. `npm run generate:api` leaves `git status` clean (no API-shape change — 2.1a's operation already exists).
- The generated `shared/api/nexus-api-client.ts` is imported only from an `entities/*/api` or `features/*/api` segment or from a hand-written sibling in `shared/api/` (AD-16 / the `no-restricted-imports` gate). Server state via TanStack Query; no global client-state store.
- Every colour / font / radius / spacing value comes from `shared/tokens` (`var(--…)` in CSS Modules) — no raw literals outside the token layer.
- The Publish action is rendered as the `apply-button` — solid `--color-accent` fill, `--color-accent-foreground` label in the `label` type step, `--radius-md` corners — and is the single primary action on the surface (DESIGN.md §Components). Card at `--radius-lg` on `--color-surface`; inputs at `--radius-sm`; field errors in `--color-danger`, `aria-describedby`-associated; the failure banner uses `--color-danger` / `--color-danger-subtle`, the success confirmation `--color-success` / `--color-success-subtle`.
- Microcopy is formal (complete sentences, terminal punctuation, no exclamation/emoji). Prescribed strings verbatim: `Your job posting has been published.` (success) and `We couldn't publish this posting. Please try again.` (network/server failure — an inline banner, with the entered title and description retained and a retry that re-submits the same values).
- Validation runs on blur (per field) and again on submit — never per keystroke. Title and description are each required and non-whitespace; the client also caps them at the same lengths 2.1a enforces (title ≤ 200, description ≤ 4000).
- The "Post a Job" nav item appears **only** for a signed-in Company; a Job Seeker and an anonymous viewer never see it (no dead / disabled nav item). Opening `/post-a-job` as a non-Company redirects to `/`.
- WCAG 2.1 AA floor unchanged: visible `:focus-visible` ring on every control, full keyboard operability, `prefers-reduced-motion` respected, focus order matches reading order.
- The HTTP-plumbing refactor is **behaviour-preserving**: every story-1.3b-ii / 1.4b auth flow keeps working and every existing frontend test stays green (updated only for moved import paths).

**Never:**
- No backend change, no migration, no OpenAPI / `nswag.json` change, no CI or Vite config change, no new dependency.
- No redirect to a posting-detail view on success — Story 2.2 owns `GET /api/job-postings/{id}` and that redirect; 2.1b shows the confirmation in place.
- No edit / delete / "my postings" list / draft — 2.1b is create-only.
- No `IIdentityApi` / `IJobPostingsApi` consumption, no company-name resolution (that is 2.2's detail view).
- No new `apply-button` shared component — the Publish button is a token-styled `<button>` in the feature slice, mirroring the auth submit; extracting a shared `apply-button` is Epic 3's concern when its stateful behaviour lands.
- No change to `RoleToggle`, `app/App.tsx` routing beyond the one added child route, or `entities/session`'s public behaviour.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Signed-in Company opens `/post-a-job` | `useSession()` resolves to `{ kind:'company' }` | the Post-a-Job card renders: a title field, a description field (`<textarea>`), a single Publish `apply-button` (accent); no other primary action | N/A |
| Nav item visibility | signed-in Company | the Primary nav shows `Search` and `Post a Job` (→ `/post-a-job`); a Job Seeker / anonymous viewer sees no `Post a Job` item | N/A |
| Session still resolving on a cold load of `/post-a-job` | `useSession()` is pending (`data === undefined`) — hard refresh, bookmark, typed URL, `AppShell` + page mount together before `me` returns | render **nothing** (`return null`) until `me` settles — no redirect, no flash — then apply the Company check below | `session.isError` also renders `null` |
| Non-Company opens `/post-a-job` | `useSession()` has **resolved** to `null` (anonymous) or `{ kind:'jobSeeker' }` | `<Navigate to="/" replace/>` — the form never renders | N/A |
| Publish with valid values | title + description non-empty, within length caps | `jobPostingsClient.create({ title, description })` is called with trimmed values (`credentials: 'include'`, `X-CSRF-TOKEN` seeded); on success the form is replaced by the confirmation `Your job posting has been published.` | N/A |
| Publish, network / server failure | `create` rejects (non-`400` or a `400` with no field `errors`) | an inline banner `We couldn't publish this posting. Please try again.`; the entered title and description are retained; a "Try again" / re-submit re-sends the same values | `toApiError(err)` → banner |
| Publish, server field-validation `400` | `create` rejects `400` with an `errors` map | each `errors` entry renders under its matching field (`title` / `description`) in `--color-danger`; no banner; values retained | `400` + `errors` → per-field |
| Field validation | blur or submit with an empty / whitespace title or description | inline `--color-danger` message below that field, `aria-describedby`-linked; submit blocked while any field is invalid; no request sent | client-side |
| Over-length field | title > 200 or description > 4000 on submit | inline field error; submit blocked, no request | client-side |
| Job Seeker / anonymous reaches the endpoint anyway | (defence in depth — should be unreachable from the UI) | a `401` / `403` from `create` falls to the generic failure banner | `toApiError` → banner |
| Antiforgery token stale | `create` returns the antiforgery `400` | the shared `callWithCsrfRetry` re-seeds `GET /api/auth/csrf` and retries once; a second failure → the banner | shared retry |
| `shared` plumbing refactor | existing auth flows (register / login / logout / `me`, the antiforgery retry, `toApiError` branching) | unchanged behaviour; all existing tests pass with updated import paths only | N/A |

</frozen-after-approval>

## Resolved Decisions

- **Lift the shared HTTP plumbing into `shared/` before adding the second consumer.** `entities/job-posting/api` needs the CSRF token cache, the `credentials` / `X-CSRF-TOKEN` `fetch` wrapper, the one-shot antiforgery retry, and `toApiError` — all currently private to `entities/session/api/`. FSD forbids a clean `entities/job-posting → entities/session` dependency (slices are independent), so the plumbing moves down a layer: `toApiError` + its `ApiError` type → `shared/lib/`; the token cache + `withCredentialsAndCsrf` + `isAntiforgeryFailure` + `callWithCsrfRetry` + a `createHttp()` factory → `shared/api/http.ts` (a hand-written sibling of the generated client). `entities/session/api/authClient.ts` is rewired to consume them; `entities/session/api/{csrf.ts,toApiError.ts}` are deleted. Behaviour is identical — this is a move, not a redesign.
- **Success shows a confirmation in place, no navigation.** On a successful `create`, `CreatePostingForm` swaps the form for a `--color-success`-toned panel reading `Your job posting has been published.` (with a "Post another job" affordance that resets the form). The redirect to the posting's detail view is Story 2.2's AC and needs 2.2's detail route.
- **Non-Company `/post-a-job` access redirects to `/`** (not `/sign-in`). The nav item is Company-only so this is a typed-URL / stale-link path; sending everyone to Home is simplest and matches how `SignInPage` redirects an already-signed-in user.
- **The Publish button is a token-styled `<button>` in `features/create-posting`, not a shared component.** DESIGN.md calls `apply-button` "*the* primary-action button", but its stateful variants (Apply → Applied, apply-gate) are Epic 3; 2.1b mirrors the 1.3b-ii auth submit (same `--color-accent` / `--radius-md` / `label` type-step values in a CSS Module).
- **Description is a `<textarea>`**; title is a single-line `<input type="text">`.
- **The page guard waits for the session before deciding.** `PostAJobPage` returns `null` while `useSession()` is pending or errored, and only redirects a non-Company once `me` has actually resolved. Redirecting on a pending session (the original frozen matrix wording) bounced a genuinely signed-in Company off its own page on every hard refresh / bookmark / typed URL, since `AppShell` and the page mount together before `me` returns. (Human-renegotiated 2026-09-08 — see Spec Change Log.)

## Code Map

**Shared — HTTP plumbing extraction (behaviour-preserving)**
- `frontend/src/shared/lib/toApiError.ts` -- NEW (moved verbatim from `entities/session/api/toApiError.ts`): `export function toApiError(err): ApiError | null` + `export interface ApiError`. Import `ApiException` from `../api` (or `../api/nexus-api-client`).
- `frontend/src/shared/lib/index.ts` -- NEW barrel: `export { toApiError, type ApiError } from './toApiError'`.
- `frontend/src/shared/index.ts` -- add `export * from './lib'` (keeps `APP_NAME`, `./tokens`, `./ui`; still does **not** re-export `./api`).
- `frontend/src/shared/api/http.ts` -- NEW (moved from `entities/session/api/csrf.ts` + the wrapper/retry half of `authClient.ts`): the module-scoped CSRF token cache (`ensureCsrfToken` / `peekCsrfToken` / `resetCsrfToken`, using `new AuthClient('', …).csrf()` for `GET /api/auth/csrf`), `withCredentialsAndCsrf(init)`, `isAntiforgeryFailure(err)` (uses `toApiError` from `../lib`), `callWithCsrfRetry(call)`, and `createHttp(): { fetch }` returning `{ fetch: (url, init) => window.fetch(url, withCredentialsAndCsrf(init)) }`. Imports `AuthClient` / `ApiException` from `./nexus-api-client` (the `no-restricted-imports` group matches the string `shared/api`, which `./nexus-api-client` is not — verify `npm run lint` stays green).
- `frontend/src/shared/api/index.ts` -- add `export * from './http'` alongside the existing `export * from './nexus-api-client'`. Update the header comment (it now also carries the hand-written `http` helpers).

**Entities — session rewire**
- `frontend/src/entities/session/api/authClient.ts` -- rewrite to `import { createHttp, callWithCsrfRetry } from '../../../shared/api'` and build `new AuthClient('', createHttp())`; keep the thin `{ register, login, logout, me }` (the three mutating ones through `callWithCsrfRetry`). Drop the local `withCredentialsAndCsrf` / `isAntiforgeryFailure` / `callWithCsrfRetry` / CSRF-cache code and the `./csrf` / `./toApiError` imports.
- `frontend/src/entities/session/api/csrf.ts` -- DELETE (moved to `shared/api/http.ts`).
- `frontend/src/entities/session/api/toApiError.ts` -- DELETE (moved to `shared/lib/toApiError.ts`).
- `frontend/src/entities/session/model/sessionQuery.ts` -- `import { toApiError } from '../../../shared'` (was `../api/toApiError`).
- `frontend/src/entities/session/index.ts` -- drop the `toApiError` / `ApiError` re-export (now from `shared`); keep `authClient`, `sessionQueryKey`, `useSession`, `SessionViewer`.
- `frontend/src/features/auth/model/useAuthForm.ts` -- `import { toApiError } from '../../../shared'`; keep `authClient`, `sessionQueryKey` from `../../../entities`.

**Entities — new `job-posting` slice**
- `frontend/src/entities/job-posting/api/jobPostingsClient.ts` -- NEW. `import { JobPostingsClient, createHttp, callWithCsrfRetry, type CreateJobPostingRequest, type JobPostingResponse } from '../../../shared/api'`; `const rawClient = new JobPostingsClient('', createHttp())`; `export const jobPostingsClient = { create: (body: CreateJobPostingRequest): Promise<JobPostingResponse> => callWithCsrfRetry(() => rawClient.create(body)) }`; re-export the two types.
- `frontend/src/entities/job-posting/index.ts` -- NEW barrel: `export { jobPostingsClient, type CreateJobPostingRequest, type JobPostingResponse } from './api/jobPostingsClient'`.
- `frontend/src/entities/index.ts` -- add `export * from './job-posting'`.

**Features — new `create-posting` slice**
- `frontend/src/features/create-posting/model/useCreatePostingForm.ts` -- NEW, mirroring `useAuthForm`: `values { title, description }`, `fieldErrors`, `formError`, `published` boolean; `validateTitle` / `validateDescription` (non-empty/non-whitespace, ≤ 200 / ≤ 4000 — messages the agent's, formal); `blurField` / `setField` (blur/submit-only validation, clear stale error on edit); `useMutation` over `jobPostingsClient.create` with trimmed values; `onSuccess` → `setPublished(true)`; `onError` → `toApiError(err)`: `400` + `errors` → map `title` / `description` keys to `fieldErrors`; anything else → `setFormError('We couldn't publish this posting. Please try again.')` (keep `values`); `handleSubmit` validates then `mutation.mutate()`; a `reset()` that clears `values` / errors / `published` (for "Post another job"); expose `ids` for `aria-describedby`.
- `frontend/src/features/create-posting/ui/CreatePostingForm.tsx` -- NEW. When `published`: a `role="status"` success panel — `Your job posting has been published.` + a "Post another job" `<button>` calling `reset()`. Otherwise a `<form onSubmit={handleSubmit} noValidate>`: heading `Post a job`; the title `<input type="text">` and description `<textarea>` (label, value, `onChange`/`onBlur`, `aria-invalid`/`aria-describedby`, inline `error` `<p>`); the failure banner (`formError`, `role="alert"`) above the submit; the Publish `<button type="submit" disabled={isSubmitting}>` styled as `apply-button`.
- `frontend/src/features/create-posting/ui/CreatePostingForm.module.css` -- NEW, tokens only. Reuse the `AuthForm.module.css` shapes: `.card` (`--radius-lg` / `--color-surface` / `--color-border` / `--space-6`), `.field` / `.label` / `.input` / `.input-invalid` / `.error`, `.submit` (`--color-accent` / `--radius-md` / `label` type step), plus `.banner` (`--color-danger` text on `--color-danger-subtle`, `--radius-md`) and `.confirmation` (`--color-success` on `--color-success-subtle`).
- `frontend/src/features/create-posting/index.ts` -- NEW: `export { CreatePostingForm } from './ui/CreatePostingForm'`.
- `frontend/src/features/index.ts` -- add `export { CreatePostingForm } from './create-posting'`.

**Pages / shell / route**
- `frontend/src/pages/post-a-job/PostAJobPage.tsx` -- NEW. `const session = useSession()`; if `session.data?.kind !== 'company'` return `<Navigate to="/" replace/>`; else render `<CreatePostingForm/>` (the shell's `Container` already wraps the `<Outlet/>`).
- `frontend/src/pages/post-a-job/index.ts` -- NEW: `export { PostAJobPage } from './PostAJobPage'`.
- `frontend/src/pages/index.ts` -- add `export { PostAJobPage } from './post-a-job'`.
- `frontend/src/app/App.tsx` -- add `{ path: 'post-a-job', element: <PostAJobPage /> }` to the layout route's `children` (before the `*` not-found child).
- `frontend/src/widgets/app-shell/NavBar.tsx` -- `navItemsFor`'s `case 'company'` returns `[{ label: 'Search', to: '/' }, { label: 'Post a Job', to: '/post-a-job' }]`; `jobSeeker` and `anonymous` unchanged. Comment update.

**Tests**
- `frontend/src/shared/lib/toApiError.test.ts` -- NEW (or move the existing coverage): `toApiError` maps an `ApiException`, a ProblemDetails-shaped object, and returns `null` otherwise.
- `frontend/src/shared/api/http.test.ts` -- NEW: `ensureCsrfToken` caches (one `GET /api/auth/csrf`), `resetCsrfToken` re-seeds, `withCredentialsAndCsrf` adds `X-CSRF-TOKEN` on `POST` only and `credentials: 'include'` always, `callWithCsrfRetry` re-seeds + retries once on the antiforgery `400` and rethrows otherwise. (Port the assertions from `entities/session/api/authClient.test.ts`'s CSRF section.)
- `frontend/src/entities/session/api/authClient.test.ts` -- update imports (`resetCsrfToken` etc. now from `shared/api`); keep the behavioural assertions.
- `frontend/src/entities/session/model/sessionQuery.test.tsx`, `frontend/src/features/auth/ui/AuthForm.test.tsx`, `frontend/src/app/App.test.tsx` -- update any `toApiError` / mock paths; assertions unchanged.
- `frontend/src/entities/job-posting/api/jobPostingsClient.test.ts` -- NEW: `create` sends `POST /api/job-postings` with `credentials: 'include'` + a seeded `X-CSRF-TOKEN`, resolves the `JobPostingResponse`, and re-seeds + retries once on an antiforgery `400`.
- `frontend/src/features/create-posting/ui/CreatePostingForm.test.tsx` -- NEW: renders the two fields + one accent submit; blur/submit validation (not per keystroke); empty title/description block submit with inline errors and send no request; a successful `create` shows `Your job posting has been published.` and "Post another job" resets; a network failure shows the banner with values retained and a retry re-submits the trimmed values; a `400` with `{ errors: { title: [...] } }` renders under the title field.
- `frontend/src/widgets/app-shell/NavBar.test.tsx` -- the signed-in Company case now asserts `Search` + `Post a Job` (→ `/post-a-job`); the Job Seeker case still asserts `Search` only.
- `frontend/src/app/App.test.tsx` -- add: a signed-in Company at `/post-a-job` renders the Post-a-Job card; a Job Seeker (and anonymous) at `/post-a-job` is redirected to `/`.

**Reference — do not change**
- `frontend/src/shared/api/nexus-api-client.ts` (generated), `frontend/eslint.config.js` (the `no-restricted-imports` override already covers `entities/*/api` / `features/*/api`; `shared/api/http.ts` is exempt by not matching the pattern string), `frontend/src/shared/ui/RoleToggle.tsx`, `frontend/src/features/auth/ui/AuthForm.tsx`.
- `_bmad-output/implementation-artifacts/spec-1-3b-ii-frontend-sign-in-surface.md` -- continuity: the `useAuthForm` shape being mirrored, the `toApiError` branching contract, the `callWithCsrfRetry` semantics, the `apply-button` token values, and the `NavBar` / `App.test.tsx` test patterns.
- `_bmad-output/implementation-artifacts/spec-2-1a-job-postings-create-endpoint.md` -- continuity: `POST /api/job-postings` returns `{ id, title, description, createdAt }`; `400` validation carries an `errors` map keyed `Title` / `Description`; `401` / `403` for a non-Company; the antiforgery `400` shape the shared retry keys on.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/shared/lib/{toApiError.ts,index.ts}` + `frontend/src/shared/index.ts` -- move `toApiError` + `ApiError` to `shared/lib`; re-export from `shared`.
- [x] `frontend/src/shared/api/{http.ts,index.ts}` -- move the CSRF token cache + `withCredentialsAndCsrf` + `isAntiforgeryFailure` + `callWithCsrfRetry` + `createHttp()`; re-export from `shared/api`.
- [x] `frontend/src/entities/session/api/authClient.ts` (+ delete `csrf.ts`, `toApiError.ts`), `model/sessionQuery.ts`, `index.ts`, `frontend/src/features/auth/model/useAuthForm.ts` -- rewire imports to `shared`; behaviour unchanged.
- [x] `frontend/src/entities/job-posting/{api/jobPostingsClient.ts,index.ts}` + `frontend/src/entities/index.ts` -- the configured `JobPostingsClient` (`createHttp()` + `callWithCsrfRetry`), barrel, layer re-export.
- [x] `frontend/src/features/create-posting/{model/useCreatePostingForm.ts,ui/CreatePostingForm.tsx,ui/CreatePostingForm.module.css,index.ts}` + `frontend/src/features/index.ts` -- the form hook, the UI (form + confirmation + banner), token-only CSS, barrels.
- [x] `frontend/src/pages/post-a-job/{PostAJobPage.tsx,index.ts}` + `frontend/src/pages/index.ts` + `frontend/src/app/App.tsx` -- the guarded page and the `/post-a-job` route.
- [x] `frontend/src/widgets/app-shell/NavBar.tsx` -- the Company-only "Post a Job" nav item.
- [x] `frontend/src/shared/lib/toApiError.test.ts` + `frontend/src/shared/api/http.test.ts` -- cover the moved plumbing (port from `authClient.test.ts`).
- [x] `frontend/src/entities/job-posting/api/jobPostingsClient.test.ts` + `frontend/src/features/create-posting/ui/CreatePostingForm.test.tsx` -- cover every I/O-matrix row (validation timing, both prescribed strings, `400`-field-mapping, success confirmation + reset, failure-banner retention + retry, credentials/CSRF on the request).
- [x] `frontend/src/{entities/session/api/authClient.test.ts,entities/session/model/sessionQuery.test.tsx,features/auth/ui/AuthForm.test.tsx,widgets/app-shell/NavBar.test.tsx,app/App.test.tsx}` -- update import paths; add the Company `Post a Job` nav assertion and the `/post-a-job` render / redirect cases.

**Acceptance Criteria:**
- Given a signed-in Company, when the shell renders, then the Primary nav shows a `Post a Job` item linking to `/post-a-job`, and no such item appears for a Job Seeker or an anonymous viewer.
- Given a signed-in Company at `/post-a-job` with a valid title and description, when they click Publish, then `POST /api/job-postings` is called with the trimmed values (`credentials: 'include'`, a seeded `X-CSRF-TOKEN`), and on success the surface shows `Your job posting has been published.`.
- Given the Post-a-Job form, when the submit fails on the network or a non-field server error, then the entered title and description are retained, the banner `We couldn't publish this posting. Please try again.` is shown, and a retry re-submits the same values.
- Given a non-Company (Job Seeker or anonymous) navigating to `/post-a-job`, when the route resolves, then they are redirected to `/` and the form never renders.
- Given the CI frontend gates (`lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `npm test`, `npm run build`), when they run, then all pass; `npm run generate:api` leaves `git status` clean; and every pre-existing test passes with only moved-import changes (the HTTP-plumbing refactor changed no behaviour).

## Implementation Notes

## Spec Change Log

### 2026-09-08 — pending-session guard (intent_gap loopback, review pass 1)

- **Triggering finding:** all three review layers flagged that `PostAJobPage`'s `session.data?.kind !== 'company'` guard redirects to `/` while `useSession()` is still pending, so a signed-in Company is bounced off `/post-a-job` on every cold load (hard refresh, bookmark, typed URL).
- **Root cause (frozen):** the I/O & Edge-Case Matrix row "Non-Company opens `/post-a-job`" lumped "(or still pending)" in with anonymous / Job Seeker → `<Navigate to="/" replace/>`, conflating "unknown" with "known-not-Company".
- **Human resolution (2026-09-08):** wait, then decide. While `useSession()` is pending or errored, `PostAJobPage` renders `null` — no redirect, no flash; the Company check runs only once `me` has resolved. The matrix now has a dedicated "session still resolving" row; the "Non-Company" row is scoped to a **resolved** `null` / `jobSeeker`.
- **Known-bad state avoided:** a signed-in Company unable to deep-link / bookmark / refresh its own Post-a-Job page.
- **KEEP:** everything else in the approved implementation stands — the loopback question was one matrix cell, so the code is amended in place (one extra `patch`) rather than reverted and re-derived. The `features/create-posting`, `entities/job-posting`, and `shared/` HTTP-plumbing extraction all carry forward unchanged.

## Review Triage Log

### Pass 1 (2026-09-08) — blind-hunter, edge-case-hunter, verification-gap

1 `intent_gap` (F-PENDING — awaiting the human), 7 `patch`, 1 consolidated `defer`, the rest rejected. Local re-verification (`lint`, all gates, `vitest --run` = 113 / 12 files, `build`, `generate:api` idempotent) was green before the pass.

**Cascade:** the `intent_gap` triggered a loopback (`review_loop_iteration` 0 → 1). Human resolved it the same day (see Spec Change Log) with a one-cell matrix correction, so the code is amended in place — F-PENDING becomes an 8th `patch` and no code is reverted / re-derived. The remaining `patch` / `defer` entries stand.

**intent_gap → resolved → patch (F-PENDING):**
- `PostAJobPage` (blind-hunter, edge-case-hunter, verification-gap) — `verdict: high`. The guard `session.data?.kind !== 'company'` redirects to `/` whenever `useSession()` is **pending** (`data === undefined`), which is every cold load of `/post-a-job` (hard refresh, bookmark, typed URL) — `AppShell` and `PostAJobPage` mount together, so `me` has not resolved on the first render. A genuinely signed-in Company is bounced to `/` and never reaches its own Post-a-Job page on a cold load. Root cause was inside `<frozen-after-approval>` (the I/O-matrix row lumped "(or still pending)" with anonymous / Job Seeker). **Human resolution (2026-09-08):** "wait, then decide" — `PostAJobPage` returns `null` while `session.isPending` or `session.isError`, and applies the Company check only once `me` resolves. Frozen matrix updated (dedicated "session still resolving" row; "Non-Company" row scoped to a resolved `null` / `jobSeeker`). Fix: add `if (session.isPending || session.isError) return null` above the existing guard, plus a test that a Company at `/post-a-job` with an unseeded session cache does **not** redirect (renders the card once `me` resolves) and a pending render emits no `<Navigate>`.

**patch:**
- `CreatePostingForm` — on the success transition the form unmounts and a fresh `role="status"` node mounts already populated (unreliable SR announcement) and focus is lost from the now-gone Publish button (blind-hunter ×2). Fix: focus a `tabIndex={-1}` confirmation heading via `useEffect` when `published` flips; `reset()` returns focus to the Title input.
- `shared/api/index.ts` (blind-hunter) — `export * from './http'` publishes `peekCsrfToken` / `ensureCsrfToken` / `withCredentialsAndCsrf` / `isAntiforgeryFailure` on the `shared/api` barrel; consumers need only `createHttp` / `callWithCsrfRetry` / `resetCsrfToken`. Fix: named re-export (`http.test.ts` imports `./http` directly and is unaffected).
- `useAuthForm.ts`, `sessionQuery.ts`, `useCreatePostingForm.ts` (blind-hunter) — import `toApiError` from the top-level `../../../shared` barrel, which pulls `shared/ui` (React) + `shared/tokens` into model code. Fix: import from `../../../shared/lib` (the subpath `http.ts` already uses).
- `CreatePostingForm.tsx` (blind-hunter) — no `maxLength` on the title / description controls, so a user types past the 200 / 4000 caps and only learns on blur. Fix: `maxLength={200}` / `maxLength={4000}`.
- `CreatePostingForm.test.tsx` (verification-gap main; blind-hunter) — the max-length and server-`400`-field-mapping paths are pinned only for `title`; the structurally-identical `description` branches (`DESCRIPTION_MAX`, the `description` arm of `mapServerFieldErrors`) never run with a triggering value. Fix: a 4001-char-description case and an `errors: { Description: [...] }` case.
- `entities/index.ts` (blind-hunter) — `./job-posting` is re-exported with `export *` while `./session` uses an explicit name list; a future internal export would leak onto the `entities` surface. Fix: explicit named re-export.
- `useCreatePostingForm.handleSubmit` (edge-case-hunter) — no `if (isSubmitting) return` guard, so a fast double-click can fire two `POST /api/job-postings` before the button disables → two postings (create is not idempotent, unlike auth). Fix: the guard.

**defer:**
- Frontend HTTP-plumbing hardening on the newly-shared `shared/api/http.ts` (blind-hunter, edge-case-hunter): `ensureCsrfToken` has no in-flight coalescing (concurrent mutating calls each `GET /api/auth/csrf` — harmless, both tokens valid for the same cookie; raised + rejected on 1.3b-ii, now shared); `withCredentialsAndCsrf` adds the token on `POST` only (fine today — the API has no `PUT`/`PATCH`/`DELETE`); the CSRF `GET` goes through `AuthClient` rather than a bare `fetch` (a shared module coupled to one generated domain client); no focus management on form state transitions (applies to `AuthForm` too). → `deferred-work.md`.

**rejected:**
- blind-hunter — no `:focus-visible` rules in `CreatePostingForm.module.css`: the global `base.css` `*:focus-visible` outline (story 1.2) covers every control, exactly as for `AuthForm` (which also has no local focus rules); `test:tokens-gate` passes.
- blind-hunter — `http.ts` hard-codes `AuthClient` for the CSRF `GET`: a verbatim move of shipped 1.3b-ii code; `AuthClient.csrf()` is the typed way to hit `/api/auth/csrf` (a bare-`fetch` rewrite drops the generated typing). Folded into the `defer`.
- blind-hunter — the `shared/api → shared/lib` dependency is undocumented: `shared/lib` is dependency-free and conceptually below `shared/api`; a valid intra-`shared` import, worth one comment line, not a defect.
- blind-hunter — no busy label / `aria-busy` on Publish while submitting: mirrors the shipped `AuthForm` submit (`disabled={isSubmitting}` + `opacity: 0.7`); a "Publishing…" label is a repo-wide enhancement.
- blind-hunter — silent anonymous redirect with no "sign in to post" message: matches `SignInPage`'s silent redirect; the nav never advertises `/post-a-job` to anonymous viewers, so it is a typed-URL path.
- edge-case-hunter — `withCredentialsAndCsrf` doesn't handle `PUT`/`PATCH`/`DELETE`: the API has no such endpoints in v1; the first non-`POST` mutation's story adds the handling. Folded into the `defer`.
- edge-case-hunter — `mapServerFieldErrors` breaks if a server `errors` value is a `string` not `string[]`: RFC 9457 / ASP.NET `ValidationProblem` always produces `Record<string, string[]>` (2.1a's filter included); `messages[0]` is correct and mirrors `useAuthForm`.

## Design Notes

- **Why the plumbing moves now.** The CSRF token cache must be a single module-scoped instance (one `X-CSRF-TOKEN` per session) — it cannot be duplicated per slice. `toApiError` is a pure, dependency-free normaliser every API error branch needs. Both belong in `shared/` the moment a second slice consumes them; `entities/job-posting` is that second slice. The move keeps `entities/session` and `entities/job-posting` independent (no cross-slice import) and is behaviour-identical.
- **`shared/api/http.ts` and the import gate.** The `no-restricted-imports` rule blocks import *specifiers* containing `shared/api`. `http.ts` sits inside `shared/api/` and imports `./nexus-api-client` (a relative specifier that does not contain `shared/api`), so it is not blocked; `entities/*/api` and `features/*/api` reach `createHttp` / `callWithCsrfRetry` through `shared/api`, which their override already permits. Confirm with `npm run lint` + `npm run test:api-import-gate`.
- **Success is a state, not a route.** `CreatePostingForm` holds a `published` flag; on success it renders the confirmation panel in place. Story 2.2 will change this to `navigate` to the new posting's detail route once that route exists — 2.1b deliberately stops at the confirmation the Story 2.1 AC names.
- **The page guard vs the endpoint guard.** `PostAJobPage` redirects a non-Company to `/` so the form never shows; the endpoint's own `401` / `403` (2.1a) is the real enforcement and is still exercised by a matrix row (a stale session that changes between load and submit falls to the banner).
- **Length caps.** The client caps title at 200 / description at 4000 to match 2.1a's `[StringLength]`; a value that somehow passes the client and fails the server comes back as a `400` with an `errors` map and is shown under the field.

## Verification

**Commands:**
- `cd frontend && npm ci` -- clean install (lockfile unchanged).
- `npm run lint && npm run test:fsd-gate && npm run test:api-import-gate` -- FSD + AD-16 boundaries clean (including `shared/api/http.ts` and the new `entities/job-posting/api` / `features/create-posting` slices).
- `npm run lint:tokens && npm run test:tokens-gate` -- no raw hex / `px` outside `shared/tokens`.
- `npm test -- --run` -- all specs pass, including the new `shared/api/http`, `shared/lib/toApiError`, `jobPostingsClient`, and `CreatePostingForm` suites and the updated `authClient` / `sessionQuery` / `AuthForm` / `NavBar` / `App` suites.
- `npm run build` -- `tsc -b` + `vite build` succeed.
- `npm run generate:api && git status --porcelain src/shared/api` -- empty (only `http.ts` / `index.ts` are hand-written and unaffected by regeneration).

**Manual checks:**
- `docker compose up`; sign in as a Company → the nav shows `Post a Job`; open it, fill a title and description, Publish → `Your job posting has been published.`; "Post another job" clears the form. Inspect `job_postings.job_posting` for the new row with `owner_company_id` = the Company's id.
- Publish with an empty title → inline error on blur/submit, no request. Publish while offline (or stop the Host) → the banner appears, the fields keep their text, and a retry re-sends.
- Sign in as a Job Seeker and type `/post-a-job` → redirected to `/`, no form. Sign out and type `/post-a-job` → redirected to `/`.
- Keyboard: Tab to `Post a Job`, then through the title / description / Publish; every control shows a focus ring; OS reduced-motion suppresses any transition.
