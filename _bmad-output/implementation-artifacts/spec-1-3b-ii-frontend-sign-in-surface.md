---
title: 'Frontend Sign up / Log in surface for Company accounts'
type: 'feature'
created: '2026-09-07'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'd5708271d123a66b291a54586dfbfdd2564d2b1e'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/EXPERIENCE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/mockups/key-auth.html'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Stories 1.3a and 1.3b-i shipped the `/api/auth/*` endpoints and the generated `shared/api` TypeScript client, but the SPA cannot use them: the nav's "Sign up / Log in" link targets `/sign-in`, which has no route and renders the not-found surface, and the shell only ever renders the signed-out state. A Company cannot register or sign in through the app.

**Approach:** Add TanStack Query in `app/`; an `entities/session` slice that owns the `GET /api/auth/me` query and a configured `AuthClient` (same-origin, `credentials: 'include'`, `X-CSRF-TOKEN` seeded from `GET /api/auth/csrf` on mutating calls); a `features/auth` slice with the role-toggle sign-up / log-in form and its register / login mutations; a `pages/sign-in` screen wired at `/sign-in`; and a widened role-aware shell that renders the signed-in Company nav. Add the Vite dev-server `/api` proxy so `npm run dev` reaches the Host.

## Boundaries & Constraints

**Always:**
- FSD downward imports only; `npm run lint`, `npm run test:fsd-gate`, `npm run lint:tokens`, `npm run test:tokens-gate`, `npm test`, `npm run build` all stay green. The generated `shared/api/nexus-api-client.ts` is imported only from an `entities/*/api` or `features/*/api` segment (AD-16).
- Every colour / font / radius / spacing value comes from `shared/tokens` (`var(--…)` in CSS Modules, `tokens.ts` for inline style) — no raw literals outside the token layer.
- Server state is TanStack Query only; no global client-state store. The `me` query key is owned by `entities/session` (AD-16). One `QueryClient`, created in `app/` and provided via `QueryClientProvider`; code below `app/` reaches it only through `useQueryClient()` / hooks — never by importing the `app/` singleton (that would be an upward FSD import).
- The `AuthClient` is constructed with `baseUrl: ''` (same-origin, AD-12) and an injected `http` wrapper that sets `credentials: 'include'` on every call and adds `X-CSRF-TOKEN` (from `GET /api/auth/csrf`) to `register` / `login` / `logout`. `nswag.json` and `nexus-api-client.ts` are not edited — regenerate only, no drift.
- Microcopy is formal: complete sentences, terminal punctuation, no exclamation marks or emoji. Use these two strings verbatim: `This email is already registered as a Company.` (duplicate registration — inline under the email field, other entered values retained) and `That email and password don't match. Please try again.` (failed sign-in).
- Validation runs on blur (per field) and again on submit — never per keystroke. Errors render inline below the field in the `danger` token and are `aria-describedby`-associated with the field.
- WCAG 2.1 AA floor: visible `:focus-visible` ring on every control, full keyboard operability, focus order matches reading order, `prefers-reduced-motion` suppresses transitions.
- The signed-in Company `NavBar` never shows a dead or disabled link to an Epic-2 surface (no "Post a Job", no "My Postings").
- `accountType` sent to the API is the literal `"company"`.

**Never:**
- No Job Seeker path: the sign-up form defaults to Company; Job Seeker is visible but disabled with a short "coming soon" note. No `job_seeker` handling — that is story 1.4.
- No backend code, no migration, no OpenAPI / document change, no change to any `/api/auth/*` endpoint behaviour.
- No password-strength meter, "remember me", social login, password reset, or email verification.
- No apply-gate modal (Epic 3); `RoleToggle` is composed here but the modal reuse is out of scope.
- No new global CSS, no responsive breakpoints, light mode only.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Anonymous opens `/sign-in` | no session cookie | Shell + auth card: role toggle (Company active, Job Seeker disabled + "coming soon" note), sign-up mode by default with a required "Company name" field above email, then email and password, one accent submit, a mode-switch link to log in | N/A |
| Switch to log-in mode | click the mode-switch link | Name field removed; heading + submit relabel; entered email / password retained; field errors cleared | N/A |
| Successful registration | valid unique email + password (≥ 8 chars), Company | `register` resolves; `me` query invalidated and refetched; shell re-renders to the signed-in Company state; navigate to `/` | N/A |
| Successful sign-in | matching email + password | `login` resolves; `me` invalidated / refetched; shell signed-in; navigate to `/` | N/A |
| Duplicate-email registration | email already a Company → `409` | `This email is already registered as a Company.` under the email field; name + password retained; stay on `/sign-in` | `toApiError(err)?.status === 409` → email-field error |
| Failed sign-in | wrong email or password → `401` | `That email and password don't match. Please try again.` as a form-level error near the submit, not tied to a field | `toApiError(err)?.status === 401` → generic message |
| Field validation | blur or submit with an empty / malformed field | inline `danger` message below that field, `aria-describedby`-linked; submit blocked while any field is invalid | client-side, no request sent |
| Antiforgery token missing / stale | a mutating call returns `400` antiforgery | clear the cached token, re-fetch `GET /api/auth/csrf`, retry the call once; a second failure surfaces the generic form error | wrapper refetches token once |
| `GET /api/auth/me` on load, no cookie | `401` | treated as "no viewer" (`null`) — signed-out shell, no error boundary, no retry | query fn returns `null` when `toApiError(err)?.status === 401` |
| Signed-in Company opens `/sign-in` | valid cookie, `me` resolves | redirect to `/` (`<Navigate replace>`) — no reason to re-authenticate | N/A |
| Signed-in Company nav | `me` resolves to `{ kind:'company', displayName }` | NavBar shows `Search`, the display name, and a `Log out` control; `Log out` calls `logout`, invalidates `me`, returns to the signed-out shell at `/` | N/A |
| `me` query pending | first paint before `me` settles | render the anonymous shell (no flash of a Log out affordance) | N/A |
| `npm run dev` calls `/api/*` | Vite dev server up, Host on `http://localhost:2052` | request proxied to the Host; the auth cookie round-trips | proxy `changeOrigin` |

## Resolved Decisions

- **The disabled "Job Seeker (coming soon)" option extends `shared/ui/RoleToggle`** (human decision). Add an optional `disabledValues?: Role[]` prop: a disabled option renders with `aria-disabled`, is skipped by arrow-key navigation and click selection, and carries a muted disabled style in `RoleToggle.module.css`. `features/auth` passes `disabledValues={['jobSeeker']}` and defaults selection to `company`. The change is additive and backward-compatible; story 1.4 drops the prop usage to re-enable Job Seeker. The "coming soon" note itself is rendered by `features/auth` beneath the toggle (formal copy, e.g. `Job Seeker accounts are coming soon.`).
- **Spec kept whole despite ~3,700+ tokens** (human decision): one cohesive user goal spanning 6 FSD layers that are tightly coupled and resist a clean split. Context-rot risk is accepted and mitigated by the detailed Code Map and per-file tasks.

</frozen-after-approval>

## Code Map

- `frontend/src/shared/api/nexus-api-client.ts` -- generated `AuthClient` (ctor `(baseUrl?, http?)`, `http` defaults to `window`), methods `register` / `login` / `me` / `logout` / `csrf`; interfaces `RegisterRequest {accountType,name,email,password}`, `LoginRequest {accountType,email,password}`, `AuthAccountResponse {id,accountType,displayName}`, `CsrfTokenResponse {token}`, `ProblemDetails`, `ApiException {status,result}`. Reuse via the `shared/api` barrel; do not edit.
- `frontend/src/app/App.tsx` -- wrap `<RouterProvider>` in `<QueryClientProvider client={queryClient}>`; the layout route element becomes `<AppShell/>` with no `viewer` prop (AppShell derives it from `useSession()` — see below); add a real child route `{ path: 'sign-in', element: <SignInPage/> }` (no longer the `*` not-found seam). Keep the exported `routes`. `RouteError` unchanged.
- `frontend/src/app/queryClient.ts` -- NEW. `new QueryClient({ defaultOptions: { queries: { retry: false } } })` — a `401` from `me` is expected, not transient.
- `frontend/src/entities/session/` -- NEW. `api/authClient.ts` (the only `entities/*` import of the generated client: build the raw `AuthClient` from `shared/api` with `baseUrl:''` and an `http` wrapper that adds `credentials: 'include'` on every call and injects the cached `X-CSRF-TOKEN` header on `POST`s; then export a thin object — `register` / `login` / `me` / `logout` — whose `register` / `login` / `logout` wrap the raw call in `callWithCsrfRetry` (catch → if `toApiError(err)` is antiforgery-`400` then `resetCsrfToken()`, re-seed, retry once, else rethrow). The antiforgery retry lives here at the method level, not in the `fetch` wrapper, which only sees `Response`s); `api/toApiError.ts` (`toApiError(err): { status: number; title?: string; errors?: Record<string,string[]> } | null` — the NSwag client throws the parsed RFC 9457 body for any 4xx that has one, and an `ApiException` only for bodiless / unexpected failures; this normaliser reads `.status` from `ApiException.isApiException(err)` first, else from a ProblemDetails-shaped `{ status:number }`, else `null`); `api/csrf.ts` (lazy `GET /api/auth/csrf` token cache + `resetCsrfToken()`); `model/sessionQuery.ts` (`sessionQueryKey = ['session','me'] as const`; `useSession()` wrapping `useQuery` with `staleTime: Infinity`; query fn calls `authClient.me()`, returns `null` when `toApiError(err)?.status === 401`, rethrows otherwise; exports a `Viewer` view type); `index.ts` barrel.
- `frontend/src/features/auth/` -- NEW. `ui/AuthForm.tsx` (RoleToggle + "Company name" field in sign-up mode + email + password, mode-switch link, blur+submit validation, `aria-describedby` errors, one accent submit); `ui/AuthForm.module.css` (tokens only — card at `--radius-lg`, inputs at `--radius-sm`, submit at `--radius-md` and `--color-accent`, per `mockups/key-auth.html`); `model/useAuthForm.ts` (field + mode state, validation rules, `register` / `login` via `useMutation` over `authClient`, success → `useQueryClient().invalidateQueries({ queryKey: sessionQueryKey })` then `navigate('/')`; on error, branch on `toApiError(err)?.status` — `409` → email-field error with the prescribed string (keep name + password), `401` → generic form error with the prescribed string, a `400` with `errors` → surface each entry under its matching field, anything else → the generic form error); `index.ts`.
- `frontend/src/pages/sign-in/` -- NEW. `SignInPage.tsx` (heading card in a `Container`, renders `<AuthForm/>`; if `useSession()` resolves to a Company, `<Navigate to="/" replace/>`), `index.ts`.
- `frontend/src/pages/index.ts` -- add `export { SignInPage } from './sign-in'`.
- `frontend/src/widgets/app-shell/NavBar.tsx` -- widen `Viewer` to `{ kind:'anonymous' } | { kind:'company'; displayName: string }`; `navItemsFor` returns `Search` (`/`) for a company; render the `displayName` and a `Log out` `<button>` (not a `NavLink`) wired via an `onLogOut` prop; no Epic-2 links. Extend `NavBar.module.css` for the name + button (token values only, reuse `--text-label-*`).
- `frontend/src/widgets/app-shell/AppShell.tsx` -- derive `viewer` from `useSession()` (`pending` or `null` → `{kind:'anonymous'}`; Company → `{kind:'company',displayName}`); the story-1.2 `viewer` prop becomes an optional override kept only for unit tests. Owns the log-out handler (`authClient.logout()` → `useQueryClient().invalidateQueries` for `me` → `useNavigate()('/')`) and passes it to `NavBar` as `onLogOut`, keeping `NavBar` presentational. `AppShell` renders inside the router + query providers, so the hooks are in scope.
- `frontend/src/widgets/app-shell/index.ts` -- re-export the widened `Viewer`.
- `frontend/src/shared/ui/RoleToggle.tsx` / `RoleToggle.module.css` / `RoleToggle.test.tsx` -- add `disabledValues?: Role[]`: disabled options get `aria-disabled`, are skipped by the arrow-key `commit` loop and ignore `onClick`, and take a muted disabled style; extend `RoleToggleProps` in `shared/ui/index.ts`; add a test that a disabled option is not selectable by click or arrow key. Everything else (selection-follows-focus, roving `tabindex`, active `--color-primary` + `--radius-full`) unchanged.
- `frontend/src/entities/index.ts`, `frontend/src/features/index.ts` -- replace the `export {}` stub with the slice re-exports.
- `frontend/eslint.config.js` -- add the AD-16 client-import restriction (deferred to "the first consumer" by 1.3b-i): a `no-restricted-imports` rule (patterns `**/shared/api`, `**/shared/api/*`, `shared/api`, `shared/api/*`) in the `src/**/*.{ts,tsx}` block, with an override that clears it for `src/entities/*/api/**` and `src/features/*/api/**`. Only `entities/session/api/authClient.ts` should be allowed to resolve it. `nexus-api-client.ts` stays in the top-level `ignores`.
- `frontend/scripts/check-api-import-gate.mjs` -- NEW (optional, mirrors `check-fsd-gate.mjs` / `check-tokens-gate.mjs`): writes a fixture that imports `shared/api` from outside an `*/api` segment, lints it, and fails unless `no-restricted-imports` reports an error. Wire a `test:api-import-gate` script + a CI step in the `frontend` job.
- `frontend/vite.config.ts` -- add `server: { proxy: { '/api': { target: 'http://localhost:2052', changeOrigin: true } } }` (the Host `launchSettings.json` HTTP URL; also clears the story-1.1 deferred "no Vite `/api` proxy" item).
- `frontend/src/app/App.test.tsx` -- update: `/sign-in` now renders the auth card, not the not-found surface; the mount tree needs a `QueryClientProvider` and a stubbed `me`.
- `frontend/src/widgets/app-shell/NavBar.test.tsx` -- add the signed-in Company case (Search + display name + Log out; no Epic-2 links); keep the anonymous assertions.
- `frontend/src/shared/ui/RoleToggle.tsx` (existing) -- `Role = 'company' | 'jobSeeker'`, `OPTIONS` order `[company, jobSeeker]`, selection-follows-focus, roving `tabindex`; reuse as-is beyond the disabled change.
- `_bmad-output/implementation-artifacts/spec-1-3a-identity-backend-and-auth-endpoints.md` -- continuity: status codes (200 / 400 / 401 / 409), generic `401` for any credential mismatch, `409` for duplicate email, cookie `nexusjob_auth` (`HttpOnly; Secure; SameSite=Lax`), `GET /api/auth/csrf` sets the antiforgery cookie and returns `{ token }`.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/package.json` + `frontend/package-lock.json` -- `npm install @tanstack/react-query@5` (a caret range in `package.json` like the other deps, the resolved version locked in `package-lock.json`). *(`^5.102.8`.)*
- [x] `frontend/src/app/queryClient.ts` + `frontend/src/app/App.tsx` -- `QueryClient` (retry off) + `QueryClientProvider`; derive `viewer` from the `session` query; add the real `/sign-in` route. *(`AppShell` takes no `viewer` prop; `/sign-in` child route added ahead of `*`.)*
- [x] `frontend/src/entities/session/*` + `frontend/src/entities/index.ts` -- `authClient` (configured `AuthClient` + `credentials` / CSRF `http` wrapper), `toApiError` normaliser, lazy CSRF token cache with reset, `sessionQueryKey` + `useSession()` (`staleTime: Infinity`, `toApiError(err)?.status === 401` → `null`), `Viewer` type, barrel.
- [x] `frontend/src/features/auth/*` + `frontend/src/features/index.ts` -- `AuthForm` (RoleToggle, sign-up "Company name" field, blur+submit validation, `aria-describedby` errors, accent submit, mode switch), `useAuthForm` (register / login mutations, success → invalidate `me` + navigate `/`, `409` → email error, `401` → generic message), CSS module, barrel.
- [x] `frontend/src/pages/sign-in/*` + `frontend/src/pages/index.ts` -- `SignInPage` (`<AuthForm/>` inside the shell's `Container`, redirect a signed-in Company to `/`).
- [x] `frontend/src/widgets/app-shell/*` -- widen `Viewer`; signed-in Company `NavBar` (Search + display name + Log out, no Epic-2 links); `AppShell` owns `handleLogOut` (calls `authClient.logout()`, then `useQueryClient().invalidateQueries` for `me`, then `useNavigate()('/')`) and passes it to `NavBar` as `onLogOut`; `index.ts` re-export carries the widened `Viewer`; `NavBar.module.css` extended.
- [x] `frontend/src/shared/ui/RoleToggle.tsx` / `.module.css` / `.test.tsx` -- add `disabledValues?: Role[]` (skip in nav via `nextEnabledIndex`, ignored on click, `aria-disabled`, muted `.option-disabled` style); `RoleToggleProps` re-export carries the new field; `features/auth` passes `['jobSeeker']` and controls the value at `company`.
- [x] `frontend/eslint.config.js` + `frontend/scripts/check-api-import-gate.mjs` + `test:api-import-gate` script + CI step -- restrict `shared/api` imports to `entities/*/api` / `features/*/api` (AD-16), with a negative gate mirroring `check-fsd-gate.mjs`.
- [x] `frontend/vite.config.ts` -- `server.proxy` `/api` → `http://localhost:2052`, `changeOrigin: true`.
- [x] `frontend/src/features/auth/ui/AuthForm.test.tsx` + `frontend/src/entities/session/model/sessionQuery.test.tsx` (+ `frontend/src/entities/session/api/authClient.test.ts`) -- cover every I/O-matrix row: validation timing (blur + submit, not keystroke), both prescribed messages, mode switch retains email / password, `409` → email field, `401` → generic form error, register + login success → invalidate + navigate, `me` `401` → `null`, non-`401` → query error, antiforgery `400` → one retry after re-seeding CSRF (and no retry on a validation `400`).
- [x] `frontend/src/app/App.test.tsx` + `frontend/src/widgets/app-shell/NavBar.test.tsx` -- updated for the real `/sign-in` route, the signed-in Company nav, the `/sign-in` redirect, and the log-out handler (`authClient.logout` + `invalidateQueries`).

**Acceptance Criteria:**
- Given the built app and an anonymous visitor, when they click the shell's "Sign up / Log in" link, then `/sign-in` renders the auth card inside the shell (not the not-found surface), with the role toggle set to Company and Job Seeker disabled and carrying a "coming soon" note.
- Given the sign-up form with valid unique credentials, when the visitor submits, then `register` is called with `accountType: "company"`, the `me` query is invalidated and refetched, the shell shows the signed-in Company nav, and the app navigates to `/`.
- Given a signed-in Company, when any page loads, then `GET /api/auth/me` populates `viewer`, the NavBar shows `Search` + the display name + `Log out`, and no `Post a Job` / `My Postings` link is in the DOM.
- Given a signed-in Company, when they click `Log out`, then `POST /api/auth/logout` is called, the `me` query is invalidated, and the shell returns to the signed-out state at `/`.
- Given the CI frontend gates (`lint`, `test:fsd-gate`, `lint:tokens`, `test:tokens-gate`, `test`, `build`), when they run, then all pass — `shared/api` resolves only inside `entities/session/api` and a `no-restricted-imports` rule fails any other importer, and `npm run generate:api` leaves `git status` clean.
- Given `npm run dev` with the Host running, when the SPA calls `/api/auth/*`, then the Vite proxy forwards to the Host and the auth cookie round-trips.

## Implementation Notes

- **Shipped shape for story 1.4 to build on.** `entities/session` exports `authClient` (thin `{ register, login, logout, me }` over the generated client, `credentials: 'include'` + lazy `X-CSRF-TOKEN`, one antiforgery retry), `toApiError` (normalises the ProblemDetails-body vs `ApiException` shapes — branch on `.status`), `sessionQueryKey`, `useSession()` (`staleTime: Infinity`), and `SessionViewer` (`{ kind: 'company'; id; displayName }`). `widgets/app-shell` exports `Viewer` (`{ kind:'anonymous' } | { kind:'company'; displayName }`); `AppShell` maps one to the other. 1.4 widens both for Job Seeker and adds the `accountType` branch in `sessionQuery.fetchSession` (see `deferred-work.md`).
- **`RoleToggle` gained `disabledValues?: Role[]`** (additive). 1.4 removes `disabledValues={['jobSeeker']}` from `features/auth/ui/AuthForm.tsx` and the "Job Seeker accounts are coming soon." note to re-enable the Job Seeker path.
- **AD-16 enforcement now lives in `eslint.config.js`** — `no-restricted-imports` blocks `shared/api` outside `src/{entities,features}/*/api/**`, with the negative gate `scripts/check-api-import-gate.mjs` (`npm run test:api-import-gate`, wired into the `frontend` CI job).
- **Auth actions navigate without awaiting the `me` refetch** (`useAuthForm.onSuccess`, `AppShell.handleLogOut`): `invalidateQueries` is fire-and-forget; logout also `setQueryData(sessionQueryKey, null)` for an immediate nav clear. `handleLogOut` is best-effort (a rejected `logout()` still clears the client-side session).
- **Not run in this environment:** `npm run generate:api` drift check (needs a .NET Host build) and the manual `docker compose` / `npm run dev` end-to-end checks — no client-facing files were touched, so drift is not expected; the CI `openapi-client` job is authoritative.

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-07) — blind-hunter, edge-case-hunter, verification-gap

No intent_gap or bad_spec — no loopback. 10 `patch` entries, 2 `defer`, the rest rejected. Local re-verification (`lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `vitest --run` = 70 tests, `build`) was green before the pass.

**patch:**
- `App.test.tsx` (verification-gap main; also blind, edge deletion) — the anonymous-viewer tests dropped the only assertions pinning the Home hero note "Browsing and searching do not require an account." and the not-found page's "Return to the home page." link (`href="/"`); no other test covers them (`grep` finds those strings only in the two component sources). medium. Fix: restore both assertions.
- `useAuthForm.onSuccess` + `AppShell.handleLogOut` (blind) — both `await queryClient.invalidateQueries({ queryKey })` *before* `navigate('/')`, gating every register / login / logout on a full `GET /api/auth/me` round-trip (submit button stays disabled meanwhile); logout also does no optimistic `setQueryData(null)`. medium. Fix: navigate without awaiting the refetch; `setQueryData(sessionQueryKey, null)` on logout.
- `AppShell.handleLogOut` (blind, edge) — `try { await authClient.logout() } finally {…}` with no `catch`; a rejected `logout()` (antiforgery double-failure / 5xx) is an unhandled promise rejection from `onClick`. low. Fix: `try/catch` best-effort (swallow, still clear + navigate); add a logout-reject test.
- `AuthForm.test.tsx` (verification-gap other) — no test for the `onError` `400`-with-`errors` field-mapping branch, though the spec Code Map / Design Notes call it out. low-medium. Fix: one case rejecting with `{ status: 400, errors: { Email: [...] } }`.
- `sessionQuery.ts` + barrels + `App.test.tsx` (blind, edge claim high) — two exported types are both named `Viewer` (`entities/session` `{kind:'company';id;displayName}` vs `widgets` union); `AppShell` maps one to the other by hand. low (developer-confusion; a `features/*` importer gets the wrong shape and still typechecks). Fix: rename the `entities/session` type to `SessionViewer`.
- `entities/index.ts` + `entities/session/index.ts` (blind) — the barrels re-export the CSRF cache internals `resetCsrfToken` / `ensureCsrfToken` / `peekCsrfToken`; only `authClient.ts` (same slice, imports from `./csrf` directly) needs them. low. Fix: drop the three from both barrels.
- `authClient.ts` doc comment (blind, edge claim high) — says "The one place in `entities/*` that touches the generated client", but `csrf.ts` and `toApiError.ts` also import from `../../../shared/api`. low (misleading comment; the AD-16 rule is segment-level and satisfied). Fix: correct the wording.
- `authClient.ts` `isAntiforgeryFailure` (blind, edge) — matches *any* `400` with no `errors` map, so an unrelated bodiless `400` would reset the CSRF token and silently retry a non-idempotent POST. low (unreachable from this UI today — every reachable non-`errors` `400` on a mutating call is antiforgery). Fix: also require `/antiforgery/i.test(apiError.title ?? '')`.
- `AuthForm.tsx` (blind ×3) — (a) company-name `<input>` has no `autoComplete` (email/password do); (b) `ids.formError` is rendered as the alert's `id` but never referenced by `aria-describedby`; (c) the mode-switch `<button>` is not `disabled` while `isSubmitting` (the submit button is). low. Fix: `autoComplete="organization"`; `aria-describedby={formError ? ids.formError : undefined}` on the submit button; `disabled={isSubmitting}` on the mode-switch button.
- `RoleToggle.tsx` (edge) — clicking a disabled option still moves DOM focus onto it (a `tabIndex={-1}` button focuses on mousedown), leaving the focus ring on a dimmed, unselectable option a user naturally clicks. low. Fix: `onMouseDown={(e) => { if (disabled) e.preventDefault() }}`.

**defer:**
- `sessionQuery.fetchSession` unconditionally returns `kind: 'company'` without checking `account.accountType` (blind, edge). Not reachable now — `GET /api/auth/me` only returns a Company pre-1.4 — and story 1.4 must add the Job Seeker branch here anyway. → `deferred-work.md`.
- A non-`401` `GET /api/auth/me` failure on load leaves a signed-in user on the anonymous shell with no auto-recovery (`retry: false` + `staleTime: Infinity`; a reload re-runs the query) (blind, edge). The spec deliberately scoped `me` handling to the 401 path. low. → `deferred-work.md` (a bounded retry or an explicit error state for `me` failures).

**rejected:**
- blind/edge — `ensureCsrfToken` has no in-flight de-dup, so two concurrent mutating calls each `GET /api/auth/csrf`: the extra GET is idempotent (just re-sets the cookie) and concurrent mutating calls are not a flow this UI produces; the fix adds a module-scoped pending-promise. low.
- blind/edge — `SignInPage` shows `<AuthForm/>` for a signed-in user during the `me` pending window before redirecting: cosmetic, and only when a signed-in user manually opens `/sign-in`; guarding on `isPending` would blank the *common* anonymous cold-load before the form. low.
- blind — `vite.config.ts` proxy target `http://localhost:2052` is a hard-coded constant with no env override: the value is spec-directed, dev-only, and a wrong port fails with an obvious `ECONNREFUSED`. low.
- blind/verification-gap/edge — `check-api-import-gate.mjs` leaves a stray fixture on `SIGKILL` and only asserts the rejection direction (not that the `*/api` override still permits the import): identical shape to the shipped `check-fsd-gate.mjs`; real `npm run lint` covers the permit direction. low.
- blind/edge — the "Job Seeker accounts are coming soon." note is not `aria-describedby`-linked to the disabled radio: the disabled option is `tabIndex={-1}` and skipped by arrows, so a SR user never focuses it; the fix adds description-association surface to the shared `RoleToggle`. low.
- edge — `RoleToggle` + note render in log-in mode: **false** — matches `mockups/key-auth.html` State B and the `LoginRequest.accountType` field (login picks an account space too).
- blind — new `no-restricted-imports` key could collide with an existing one: **false** — it is the only definition in the config; `lint` and the import gate both pass.
- edge — CSRF token could be `''`/`undefined`, cached forever, sent as a literal header: **false/low** — `cachedToken` is typed `string | null` and only ever assigned `response.token` (typed `string`); 1.3a classifies `IAntiforgery.GetAndStoreTokens` returning null as unreachable.
- edge — `callWithCsrfRetry`'s re-seed `ensureCsrfToken()` throwing replaces the original antiforgery error: the user-visible outcome is the generic message either way; a double CSRF-GET failure is rare. low.
- edge — `RoleToggle` `disabledValues` containing the *selected* value yields a stuck `aria-disabled` tab stop: no caller does this (`AuthForm` disables only `jobSeeker` and selects `company`). low, undemonstrated misuse.
- edge — `useAuthForm.onError`'s `409` branch fires regardless of mode, so a login returning `409` would show "already registered": **false** — `POST /api/auth/login` has no `409` path (1.3a `LoginHandler` returns 200/401 only).
- edge — `mapServerFieldErrors` drops server `errors` keys that match no field slot and only shows `messages[0]`: reachable server `400`s (`Name`/`Email`/`Password`) all map; an unmatched key falls through to `GENERIC_MESSAGE`, not silence. low.
- edge — `check-api-import-gate.mjs` prints a raw stack if `eslint.lintFiles()` rejects: it still exits non-zero (CI fails correctly); same as `check-fsd-gate.mjs`. low.
- edge — `AppShell.viewer` went from required to optional: **false** — spec-directed test override; `useSession()` without a `QueryClientProvider` throws loudly.
- edge — `NavBar`'s optional `onLogOut` could render a `Log out` button with `onClick={undefined}`: no caller omits it (`AppShell` and both test suites pass it). low.
- edge — double-clicking `Log out` sends two logout POSTs / two `navigate('/')`: `navigate('/')` twice is a no-op and logout is ~idempotent; rare. low.
- edge — `switchMode` while a mutation is in flight lets the settled handler act for the opposite mode: the success path (`navigate('/')`) and the error messages are mode-independent; addressed anyway by disabling the mode-switch button while `isSubmitting` (patch above). low.

## Design Notes

- **Two layers around the generated client.** (1) The `AuthClient` ctor's second arg is an `http` wrapper — `{ fetch: (url, init) => window.fetch(url, { ...init, credentials: 'include', headers: withCsrf(url, init) }) }` — supplying the cookie and the `X-CSRF-TOKEN` header (`nswag.json` `withCredentials:false` must not change: drift gate). `withCsrf` adds the header only on `POST` (`/register`, `/login`, `/logout`). (2) `authClient.ts` then exports a thin `{ register, login, me, logout }` object; the three mutating methods run through `callWithCsrfRetry`. The `fetch` wrapper only sees `Response`s, so the antiforgery retry cannot live there — it lives at the method-call level where the thrown `ProblemDetails` is visible.
- **CSRF token cache.** `GET /api/auth/csrf` returns `{ token }` and sets the antiforgery cookie. Fetch lazily once, cache the token in module scope, reuse for every `POST`. `callWithCsrfRetry` treats a rejection whose `toApiError(err)` is `status: 400` with no `errors` map as an antiforgery failure (`AntiforgeryEndpointFilter` → `Results.Problem(title: "Antiforgery token validation failed.", statusCode: 400)`; a DataAnnotations `400` always carries `errors`), calls `resetCsrfToken()`, re-seeds, retries once, then gives up. `csrf()` and `me()` are GETs and need no token.
- **The NSwag client throws two different shapes — normalise with `toApiError`.** `throwException` in `nexus-api-client.ts` throws the *parsed RFC 9457 body* (a plain object: `{ type, title, status, detail, errors? }`, `status` a number) whenever the 4xx response has one — which is every `400` / `401` / `409` from `/api/auth/*` (`Results.Problem(...)` / `Results.ValidationProblem(...)`, all with a body). It throws an `ApiException` only for bodiless or unexpected responses. So `ApiException.isApiException(err)` is `false` on the auth errors. `toApiError(err)` returns `{ status, title, errors }` for both shapes (`ApiException` → its `.status`; ProblemDetails-shaped → its `.status`), else `null`. Every caller branches on `toApiError(err)?.status`, never on `instanceof` / `isApiException`.
- **`me` `401` is not an error.** When signed out, `me()` rejects with a ProblemDetails (`status: 401`, from `GetMeHandler` or the cookie handler's `OnRedirectToLogin`). The query fn returns `null` when `toApiError(err)?.status === 401`; other errors propagate. `QueryClient` retry is off so a signed-out load does not retry.
- **`useSession` uses `staleTime: Infinity`.** The session only changes on an action this app takes (register / login / logout), each of which explicitly `invalidateQueries` the key. Without this the default `staleTime: 0` refetches `me` on every component mount and every window refocus. Trade-off: a logout in another browser tab is not picked up until the next invalidation or reload — acceptable for v1 (no cross-tab session sync requirement).
- **`viewer` seam (from story 1.2).** `AppShell` / `NavBar` already select nav items by `viewer.kind`; widening the union and feeding it from `me` needs no reshaping. While `me` is `pending`, render the anonymous shell.
- **Error mapping.** Branch on `toApiError(err)?.status`: `409` → email-field error = the prescribed duplicate string, keep name + password; `401` → a single form-level error = the prescribed mismatch string, near the submit, not field-scoped (must not reveal which field was wrong); a `400` carrying `errors` (a server rule the client validation missed) → surface each `errors` entry under its matching field; anything else → the generic form error. Client validation messages are the agent's — formal, per-field, mirroring the server rules (`[EmailAddress]`, password ≥ 8, name required).
- **Test seams.** Specs mount inside a fresh `QueryClientProvider` and stub the session with `queryClient.setQueryData(sessionQueryKey, …)` (anonymous = `null`, Company = an `AuthAccountResponse`), or `vi.mock` the `entities/session` `authClient`. Mutation paths (`register` / `login` / `logout` / `csrf` / antiforgery retry) are exercised by mocking `authClient` methods and asserting the resulting DOM / navigation, not by hitting a real server.
- **Card layout** follows `mockups/key-auth.html`: `--radius-lg` card on `--color-surface` with a `--color-border` edge, `--radius-sm` inputs, `--radius-md` `--color-accent` full-width submit, `--radius-full` role toggle (unchanged). The mock's colours are illustrative; the tokens are the source of truth. Copy from the mock is the intended wording: heading `Create your account` / `Log in`, submit `Create account` / `Log in`, mode switch `Already have an account? Log in` / `New to NexusJob? Sign up`.
- **Dev proxy and the `Secure` cookie.** `/api` → `http://localhost:2052` (the Host's plain-HTTP `launchSettings` URL) works because the auth cookie is `Secure` and browsers treat `localhost` as a secure context over plain HTTP, so it is still stored and replayed through the Vite origin. If a browser ever refuses it, the fallback is `target: 'https://localhost:2051'` with `secure: false` (accept the dev self-signed cert). Not needed for `docker compose` (Host serves the built SPA same-origin).
- **`RoleToggle` disabled-skip.** With two options and `disabledValues: ['jobSeeker']`, arrow keys land only on `company` (effectively a no-op). The skip logic must terminate when every other option is disabled — scan from the next index and stop at the starting index rather than looping.

## Verification

**Commands:**
- `cd frontend && npm ci` -- clean install against the updated lockfile.
- `npm run lint` && `npm run test:fsd-gate` (&& `npm run test:api-import-gate` if added) -- FSD boundaries clean; `shared/api` reachable only from `entities/session/api`, and the negative gate proves the restriction fires.
- `npm run lint:tokens` && `npm run test:tokens-gate` -- no raw hex / `px` outside `shared/tokens`.
- `npm test -- --run` -- all specs pass, including the new `features/auth` and `entities/session` suites and the updated `App` / `NavBar` suites.
- `npm run build` -- `tsc -b` + `vite build` succeed.
- `npm run generate:api && git status --porcelain src/shared/api` -- empty (no client drift).

**Manual checks:**
- `docker compose up`, open `/sign-in`: register a new Company → lands on `/` with the display name + Log out in the nav; reload → still signed in (cookie persisted); Log out → anonymous shell.
- Re-register the same email → `This email is already registered as a Company.` under the email field, other values retained.
- Log in with a wrong password → `That email and password don't match. Please try again.` near the submit.
- Keyboard: Tab to the role toggle — arrow keys stay on Company (Job Seeker not selectable); Tab through the fields; every control shows a focus ring; OS reduced-motion suppresses the card / nav transitions.
- `npm run dev` alongside `dotnet run --project backend/NexusJob.Host`: the surface works end-to-end through the Vite `/api` proxy.
