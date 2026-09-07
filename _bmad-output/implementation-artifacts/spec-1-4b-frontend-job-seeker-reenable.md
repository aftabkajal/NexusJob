---
title: 'Frontend Job Seeker re-enable on the Sign up / Log in surface'
type: 'feature'
created: '2026-09-07'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f1cba8dbf9f399be2c1696247faad1c0fc8f0bb6'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/mockups/key-auth.html'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.3b-ii shipped the auth surface with Job Seeker shown as a **disabled** "coming soon" option, and story 1.4a shipped the backend that now actually handles `accountType: "job_seeker"` on `/api/auth/{register,login,me}`. The frontend still hardcodes `accountType: "company"`, still disables the toggle option, and `SessionViewer` / the shell only know the Company shape — so a job seeker cannot register or sign in through the app.

**Approach:** Re-enable the Job Seeker option in the role toggle, translate the selected `Role` to the `accountType` wire value in the register / login mutations, make the name-field label / `autoComplete` / duplicate-email message role-aware, clear role-specific field errors on a toggle switch, widen `SessionViewer` and the shell `Viewer` to `company | jobSeeker` (branching `fetchSession` on `account.accountType`), and give a signed-in Job Seeker the same Search + name + Log out nav a Company gets. This is story **1.4b**; the backend is **1.4a** (`spec-1-4a-job-seeker-identity-backend.md`, done).

## Boundaries & Constraints

**Always:**
- FSD downward imports only; `npm run lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `npm test`, `npm run build` all stay green. `npm run generate:api` leaves `git status` clean (no API-shape change).
- Every colour / font / radius / spacing value comes from `shared/tokens` (`var(--…)` in CSS Modules, `tokens.ts` for inline style) — no raw literals outside the token layer.
- The `Role` union stays `'company' | 'jobSeeker'` (a UI concern, camelCase). The single translation to the API's `'company' | 'job_seeker'` happens in `useAuthForm`'s `mutationFn`; `job_seeker` never appears in a component prop, in `RoleToggle`, or in the shell.
- Microcopy stays formal (complete sentences, terminal punctuation, no exclamation/emoji). Prescribed strings, role-aware: `This email is already registered as a {Company|Job Seeker}.` (duplicate registration — inline under the email field, other entered values retained) and `That email and password don't match. Please try again.` (failed sign-in — unchanged, role-agnostic, never reveals the role).
- The sign-up name field is labelled `Company name` when the toggle is on Company and `Full name` when on Job Seeker; its `autoComplete` is `organization` / `name` respectively; log-in mode never shows it. Switching the toggle clears the current field and form errors and keeps the entered email / password / name.
- The role toggle's heading (`Create your account` / `Log in`) and submit label (`Create account` / `Log in`) do **not** change with the role — only the name label, `autoComplete`, and the duplicate message do (matches `mockups/key-auth.html`, whose State B shows the toggle in log-in mode too).
- A signed-in Job Seeker `NavBar` shows Search + the display name + Log out and **no** Company-only or Job-Seeker-only item — no `Post a Job`, no `My Postings`, no `My Applications` (all Epic 2/3). Both signed-in roles render the same nav in this story.
- `fetchSession` maps `account.accountType`: `"company"` → `{ kind:'company', … }`, `"job_seeker"` → `{ kind:'jobSeeker', … }`, anything else → `throw` (the query goes to `isError`) — this closes the `deferred-work.md` `fetchSession` item.
- WCAG 2.1 AA floor unchanged: visible `:focus-visible` ring on every control, full keyboard operability, `prefers-reduced-motion` respected, field errors `aria-describedby`-associated.

**Never:**
- No backend change, no migration, no OpenAPI / generated-client change, no `nswag.json` edit, no CI or Vite config change.
- No third role, no account-linking or "switch identity" UI, no per-role heading/submit copy, no `My Applications` link (Epic 3).
- No change to `entities/session/api/*` (the `authClient` wrapper, `toApiError`, `csrf`), to `app/App.tsx` routing, or to `RoleToggle`'s behaviour — `RoleToggle` keeps its `disabledValues` prop (the Epic-3 apply-gate still wants it); 1.4b only stops passing `disabledValues={['jobSeeker']}` from `features/auth`.
- No new dependency (`@tanstack/react-query`, `react-router` already present).
- No robustness work on a non-`401` `GET /api/auth/me` failure (a separate `deferred-work.md` item).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| `/sign-in` first paint | anonymous, sign-up mode | role toggle with **both** options selectable, Company active by default; no "coming soon" note; name field labelled "Company name" | N/A |
| Switch the toggle to Job Seeker | click "Job Seeker" in sign-up mode, some fields filled / errored | option becomes active; name label → "Full name", its `autoComplete` → `name`; entered email / password / name kept; current field + form errors cleared | N/A |
| Switch back to Company | click "Company" | name label → "Company name", `autoComplete` → `organization`; values kept; errors cleared | N/A |
| Submit sign-up as Job Seeker | role = Job Seeker, valid Full name + email + password | `register` called with `accountType: "job_seeker"`; on success `me` invalidated (fire-and-forget), navigate to `/` | `409` → `This email is already registered as a Job Seeker.` under the email field, name + password kept; `400`+`errors` → per-field; else generic form error |
| Submit log-in as Job Seeker | role = Job Seeker, log-in mode, valid email + password | `login` called with `accountType: "job_seeker"`; success → invalidate `me`, navigate `/` | `401` → the generic mismatch message near the submit |
| Submit sign-up as Company | role = Company | `register` called with `accountType: "company"` (unchanged); `409` → `This email is already registered as a Company.` | as today |
| `GET /api/auth/me` resolves a Job Seeker | signed-in Job Seeker session, any page load | `useSession().data` = `{ kind:'jobSeeker', id, displayName }`; `AppShell` renders the signed-in nav (name + Log out); `navItemsFor` returns `[Search]` | N/A |
| `GET /api/auth/me` resolves a Company | signed-in Company | unchanged — `{ kind:'company', … }`, signed-in nav | N/A |
| `GET /api/auth/me` returns an unknown `accountType` | `fetchSession` receives `accountType` neither `"company"` nor `"job_seeker"` | `fetchSession` throws → `useSession()` is `isError`; `AppShell` falls back to the anonymous shell (no viewer) | thrown from the query fn |
| Signed-in Job Seeker clicks Log out | `viewer.kind === 'jobSeeker'` | `POST /api/auth/logout`, `me` invalidated + optimistically set to `null`, navigate `/` — same as a Company (handler is role-agnostic) | best-effort (unchanged) |
| Field validation, Job Seeker sign-up | blur / submit with an empty Full name | inline `danger` message "Enter your full name." below the field, `aria-describedby`-linked; submit blocked | client-side, no request |

</frozen-after-approval>

## Resolved Decisions

- **Role → wire translation lives only in `useAuthForm`'s `mutationFn`:** `accountType: role === 'jobSeeker' ? 'job_seeker' : 'company'`. Nothing else in the frontend knows the snake_case value.
- **Switching the toggle clears all field + form errors** (not a curated "role-specific" subset). The 1.4 AC says "role-specific field errors"; clearing everything and keeping the entered values is a faithful, simpler implementation and matches how `switchMode` already behaves.
- **Both signed-in roles get the identical nav** (Search + display name + Log out) in this story. `navItemsFor` gains a `jobSeeker` case returning `[Search]`; the `NavBar` signed-in block guard widens from `viewer.kind === 'company'` to `viewer.kind !== 'anonymous'`. "My Applications" is Epic 3 and linking a non-existent surface breaks the "no dead nav item" rule.
- **`SessionViewer` and the shell `Viewer` are two separate types** (as established in 1.3b-ii review): `entities/session` `SessionViewer` widens to `{ kind: 'company' | 'jobSeeker'; id; displayName }`; `widgets/app-shell` `Viewer` widens to `{ kind:'anonymous' } | { kind:'company'; displayName } | { kind:'jobSeeker'; displayName }`. `AppShell` maps one to the other.
- **Heading and submit copy do not vary by role** — only the name label, its `autoComplete`, and the duplicate-email message. Per `mockups/key-auth.html`.

## Code Map

- `frontend/src/features/auth/model/useAuthForm.ts` -- `role` state + `setRole` already exist (`useState<Role>('company')`, passed to `RoleToggle`). Changes: (1) `mutationFn` sends `accountType: role === 'jobSeeker' ? 'job_seeker' : 'company'` for both `authClient.register(...)` and `authClient.login(...)` (currently hardcoded `'company'` at the two call sites); (2) the name validator becomes role-aware — `role === 'jobSeeker' ? 'Enter your full name.' : 'Enter your company name.'` (thread `role` into `validateName`, or resolve the message where `blurField` / `handleSubmit` set the name error); (3) `DUPLICATE_EMAIL_MESSAGE` becomes a function of `role` — `This email is already registered as a Company.` / `This email is already registered as a Job Seeker.` (used in the `onError` `409` branch); (4) add `changeRole(next: Role)` that calls `setRole(next)`, `setFieldErrors({})`, `setFormError(undefined)` and leaves `values` untouched; return it from the hook in place of exposing bare `setRole`.
- `frontend/src/features/auth/ui/AuthForm.tsx` -- remove `disabledValues={['jobSeeker']}` from `<RoleToggle>` (line ~41) and the `<p className={styles.note}>Job Seeker accounts are coming soon.</p>` (line ~43); wire `<RoleToggle … onChange={changeRole}>`. Name `<label>` text: `role === 'jobSeeker' ? 'Full name' : 'Company name'` (line ~48). Name `<input>` `autoComplete`: `role === 'jobSeeker' ? 'name' : 'organization'` (line ~54). Update the component doc comment ("Sign-up mode adds a required Company name field" → role-aware). No other markup change.
- `frontend/src/features/auth/ui/AuthForm.module.css` -- delete the now-unused `.note` rule (kept only for the removed "coming soon" paragraph). `lint:tokens` and the CSS-Modules unused-class check must stay green.
- `frontend/src/entities/session/model/sessionQuery.ts` -- widen `SessionViewer.kind` to `'company' | 'jobSeeker'` (line ~19). In `fetchSession` (line ~24), replace the unconditional `{ kind: 'company', … }` with a branch on `account.accountType`: `'company'` → `{ kind:'company', id, displayName }`, `'job_seeker'` → `{ kind:'jobSeeker', id, displayName }`, `default` → `throw new Error(\`unexpected accountType: ${account.accountType}\`)`. Update the doc comment (drop "only knows the Company shape").
- `frontend/src/entities/session/model/sessionQuery.test.tsx` -- add: a resolved `job_seeker` account maps to `{ kind:'jobSeeker', … }`; an unknown `accountType` (e.g. `'admin'`) puts `useSession()` in `isError`. Keep the existing company + 401 + non-401 cases.
- `frontend/src/widgets/app-shell/NavBar.tsx` -- widen `Viewer` (line ~13) to add `| { kind: 'jobSeeker'; displayName: string }`; `navItemsFor` (line ~26) add `case 'jobSeeker': return [{ label: 'Search', to: '/' }]`; change the signed-in `<span>`+`<button>` guard from `viewer.kind === 'company'` (line ~60) to `viewer.kind !== 'anonymous'` so both roles render the display name + Log out. Comment update for the widened union.
- `frontend/src/widgets/app-shell/NavBar.test.tsx` -- add a signed-in Job Seeker case: `navItemsFor({ kind:'jobSeeker', … })` is `[{ to:'/' }]`; the rendered nav has Search + the display name + Log out and no `/post-a-job` / `/my-postings` / `/my-applications` href; `onLogOut` fires on click. Mirror the existing Company block.
- `frontend/src/widgets/app-shell/AppShell.tsx` -- the `viewer` map (line ~33) becomes `session.data ? { kind: session.data.kind, displayName: session.data.displayName } : { kind: 'anonymous' }` (was hardcoding `kind: 'company'`).
- `frontend/src/app/App.test.tsx` -- the `/sign-in` "auth card" test (line ~74) asserts `Job Seeker` has `aria-disabled` and the "coming soon" text (line ~84) — update to: both options selectable, no "coming soon" note. `renderAt`'s `SessionViewer` seed (a param default `kind: 'company'`) — add a `jobSeeker` variant test (signed-in Job Seeker gets the signed-in nav; `/sign-in` redirects them to `/` like a Company).
- `frontend/src/features/auth/ui/AuthForm.test.tsx` -- drop the "disabled Job Seeker option" / "coming soon" assertions (lines ~51-60); assert both options are selectable. Add: switching to Job Seeker relabels the name field to "Full name" and keeps entered email / password and clears field errors; submitting sign-up as a Job Seeker calls `register` with `accountType: 'job_seeker'`; submitting log-in as a Job Seeker calls `login` with `accountType: 'job_seeker'`; a Job Seeker `409` shows "This email is already registered as a Job Seeker." under the email field. Keep the Company-path cases.
- `frontend/src/shared/ui/RoleToggle.tsx` -- no code change. Tidy the `disabledValues` doc comment (line ~36): "story 1.4b drops the *usage* from `features/auth` (the prop stays for the Epic-3 apply-gate)".
- `_bmad-output/implementation-artifacts/spec-1-4a-job-seeker-identity-backend.md` -- continuity: `GET /api/auth/me` returns `{ id, accountType, displayName }` with `accountType` `"company"` or `"job_seeker"`; `displayName` is `full_name` for a Job Seeker; a Job Seeker `409` on register and `401` on login use the same generic ProblemDetails shapes the frontend already maps (`toApiError(err)?.status`).

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/features/auth/model/useAuthForm.ts` -- `mutationFn` derives `accountType = role === 'jobSeeker' ? 'job_seeker' : 'company'` for both mutations; `validateName(value, role)` role-aware; `duplicateEmailMessage(role)` replaces the constant; `changeRole` clears field + form errors and keeps values; the hook returns `changeRole` in place of `setRole`.
- [x] `frontend/src/features/auth/ui/AuthForm.tsx` + `AuthForm.module.css` -- removed `disabledValues`, the "coming soon" `<p>`, and the `.note` class; `onChange={changeRole}`; name `<label>` and `autoComplete` keyed on `role === 'jobSeeker'`.
- [x] `frontend/src/entities/session/model/sessionQuery.ts` -- `SessionViewer.kind` widened; `fetchSession` `switch`es on `account.accountType` (`company` / `job_seeker` map, `default` throws).
- [x] `frontend/src/widgets/app-shell/NavBar.tsx` + `AppShell.tsx` -- `Viewer` union += `jobSeeker`; `navItemsFor` `jobSeeker` case (`[Search]`); signed-in block guard `viewer.kind !== 'anonymous'`; `AppShell` passes `session.data.kind` through.
- [x] `frontend/src/shared/ui/RoleToggle.tsx` -- doc-comment tidy only (prop retained).
- [x] `frontend/src/features/auth/ui/AuthForm.test.tsx` -- both-options-selectable; role-switch relabel / value-retention / error-clear; Job Seeker sign-up + log-in send `accountType: 'job_seeker'`; Job Seeker `409` message; empty Full name message.
- [x] `frontend/src/entities/session/model/sessionQuery.test.tsx` -- `job_seeker` → `{ kind:'jobSeeker' }`; unknown `accountType` → `isError`.
- [x] `frontend/src/widgets/app-shell/NavBar.test.tsx` -- signed-in Job Seeker nav case (Search + name + Log out, no role-restricted hrefs, `onLogOut` fires).
- [x] `frontend/src/app/App.test.tsx` -- `/sign-in` asserts both options selectable / no "coming soon"; signed-in Job Seeker viewer redirect + nav case.

**Acceptance Criteria:**
- Given the built app and `/sign-in`, when it renders, then the role toggle shows Company and Job Seeker both selectable, Company active, with no "coming soon" note; switching to Job Seeker relabels the name field to "Full name" and keeps any entered email / password / name.
- Given the sign-up form with the toggle on Job Seeker and valid fields, when the visitor submits, then `register` is called with `accountType: "job_seeker"`, and on success `me` is invalidated and the app navigates to `/`; a `409` shows "This email is already registered as a Job Seeker." under the email field.
- Given a signed-in Job Seeker, when any page loads, then `GET /api/auth/me` yields `{ kind:'jobSeeker', … }`, the NavBar shows Search + the display name + Log out, and no `Post a Job` / `My Postings` / `My Applications` link is in the DOM.
- Given `GET /api/auth/me` resolves with an `accountType` that is neither `"company"` nor `"job_seeker"`, when `useSession()` settles, then it is `isError` and `AppShell` renders the anonymous shell.
- Given the CI frontend gates (`lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `npm test`, `npm run build`), when they run, then all pass and `npm run generate:api` leaves `git status` clean; no file outside `frontend/src/{features/auth,entities/session,widgets/app-shell,shared/ui,app}` is changed.

## Implementation Notes

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-07) — blind-hunter, edge-case-hunter, verification-gap

No intent_gap or bad_spec — no loopback. 1 `patch` group (2 findings, both `changeRole`), the rest rejected. Local re-verification (`lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `vitest --run` = 85 tests, `build`) was green before the pass.

**patch:**
- `useAuthForm.ts` `changeRole` (edge-case-hunter ×2; also the `formError`-clear coverage gap from verification-gap and blind-hunter) — `changeRole` runs `setFieldErrors({})` + `setFormError(undefined)` unconditionally, so: (a) `RoleToggle` fires `onChange` with the **already-active** role on a click or `Enter`/`Space` re-confirm (deliberate WAI-ARIA selection-follows-focus, documented in the 1.3b-ii `RoleToggle` notes), which now silently wipes the user's visible errors on a no-op; (b) if the role is toggled while a register/login request is **in flight**, the settled `onError` calls `duplicateEmailMessage(role)` with the switched value, so a `409` for the submitted account type is labelled with the other role. And no test asserts the `formError` (`role="alert"`) is cleared on a role switch — only the field-error clear is covered. low-medium. Fix: `changeRole` early-returns when `next === role || mutation.isPending`; add tests — re-confirming the current role does **not** clear a visible field error, and a `401` `role="alert"` is cleared when the role is switched.

**rejected:**
- blind-hunter — "cross-role `409` message can be factually wrong (sign up as Job Seeker with an email already a Company → told it's a Job Seeker dup)": **false** — per 1.4a, registering `job_seeker` with a company-only email returns `200`, not `409` (the independence AC + `Register_job_seeker_when_the_email_is_a_company_only_succeeds` test); a `409` on a `job_seeker` register always means a `job_seeker` duplicate, so `duplicateEmailMessage('jobSeeker')` is accurate. The in-flight-toggle subcase is real and is the `patch` above.
- blind-hunter — "`changeRole` clears all errors instead of recomputing per-role — needs AC sign-off, shouldn't be in a code comment": the trade-off is recorded verbatim in this spec's `## Resolved Decisions` and was approved at CHECKPOINT 1 (it mirrors `switchMode`). Not a defect.
- blind-hunter — "name field keeps its value across a role switch even though its meaning changes — consider clearing `values.name`": the epic Story 1.4 AC and this spec's frozen I/O matrix require "entered email / password / **name** are kept". Clearing `name` would violate the frozen intent.
- blind-hunter — "`fetchSession`'s `throw`-inside-`try`-then-`catch`-then-rethrow is convoluted and its correctness depends on un-shown code": the `catch` (unchanged from 1.3b-ii) returns `null` only for a `401` and rethrows everything else, so the new `default: throw` reaches `useSession` as `isError` — verified and covered by the new `sessionQuery.test.tsx` case (85 tests green). Moving the `accountType` check before the `try` would pull `authClient.me()` out of the catch that intentionally wraps its network error. false.
- blind-hunter / (matrix-audit note) — "an errored session silently degrades to the anonymous shell; no `isError` handling or error boundary": this is the spec's **frozen** expected behaviour for the unknown-`accountType` row ("`AppShell` falls back to the anonymous shell"), and Boundaries §Never explicitly scopes out non-`401` `me`-failure robustness (a standing `deferred-work.md` item). Intentional.
- blind-hunter — "`disabledValues` on `RoleToggle` now has zero call sites and may rot": `RoleToggle.test.tsx`'s `describe('disabledValues')` block (3 tests: `aria-disabled` + non-tab-stop, click-ignored, arrow-key-skipped) is untouched by this diff and passes in the 85-test run. The prop is retained for the Epic-3 apply-gate.
- blind-hunter — "`// job_seeker never escapes this closure` comment is inaccurate — `fetchSession` switches on the response's `accountType`": the comment is about the **request** translation; `fetchSession` mapping the API's response value into the camelCase `kind` is a separate, legitimate `entities/session` concern and does not put `job_seeker` into a component prop, `RoleToggle`, or the shell. The comment is tightened to say "on the request side" in the `patch` file. low.
- blind-hunter — "`navItemsFor` `company` and `jobSeeker` arms are verbatim duplicates": kept as separate arms deliberately — Epic 2 adds Company-only nav items and Epic 3 adds a Job-Seeker-only one, so merging now just means un-merging later. low.
- blind-hunter — "`1.4` vs `1.4b` references inconsistent across comments": after this diff the remaining refs are `RoleToggle.tsx` "Story 1.4b dropped the only usage" (correct) and `useAuthForm.ts` "the 1.4 AC" (correctly names the epic's Story 1.4 acceptance criteria, which 1.4a/1.4b implement); `AuthForm.tsx` / `NavBar.tsx` comments were rewritten to drop the stale "arrives in 1.4". Consistent.
- blind-hunter — "signed-in Job Seeker `/sign-up` route not tested": there is no `/sign-up` route — sign-up is a mode of the `/sign-in` page. N/A.
- blind-hunter — "relabel test never asserts the label reverts to 'Company name'": it does — `screen.getByLabelText('Company name')` after switching back throws if the label is not exactly that.
- blind-hunter — "select-role-then-switch-mode order not covered": `changeRole` and `switchMode` are independent (`role` vs `mode` state); the "logs in a Job Seeker" test runs exactly this order.
- blind-hunter — "no `aria-live` announcement when the role toggle changes the name label / clears errors": WCAG 2.1 AA is met — the label is programmatically correct and conveyed on field focus, and the change is a direct consequence of the user's radio activation. An `aria-live` announcement is a nice-to-have beyond the AC.

## Design Notes

- **`Role` vs `accountType`.** `RoleToggle` speaks `'company' | 'jobSeeker'` (camelCase UI union). The API's `RegisterRequest`/`LoginRequest` want `'company' | 'job_seeker'`. One ternary in `mutationFn` bridges them; keep `job_seeker` out of every component boundary so a future `RoleToggle` reuse (apply-gate) is unaffected.
- **Two `Viewer`-ish types on purpose.** `SessionViewer` (has `id`, from the query) and the shell `Viewer` (anon-or-role union, no `id`) were deliberately split during the 1.3b-ii review to stop a `features/*` importer picking the wrong shape. 1.4b widens both in the same way; `AppShell` keeps the hand map.
- **`fetchSession` throw closes a deferred item.** 1.3b-ii's review deferred "`fetchSession` blindly labels any `me` result a Company". Branching on `accountType` and throwing on the unexpected value resolves it — and `useSession`'s `retry: false` + `AppShell`'s `session.data ? … : anonymous` already degrade an errored query to the anonymous shell, so no new error UI is needed here (the *non-401 failure recovery* item stays deferred).
- **Copy source.** Name label "Full name", duplicate string "This email is already registered as a Job Seeker.", and the unchanged generic mismatch string are all from epics.md Story 1.4 / the epic microcopy rules; heading & submit stay per `mockups/key-auth.html`.

## Verification

**Commands:**
- `cd frontend && npm ci` -- clean install (lockfile unchanged).
- `npm run lint && npm run test:fsd-gate && npm run test:api-import-gate` -- FSD + AD-16 boundaries clean.
- `npm run lint:tokens && npm run test:tokens-gate` -- no raw hex / `px` outside `shared/tokens`; the removed `.note` class leaves no dangling reference.
- `npm test -- --run` -- all specs pass, including the updated `AuthForm` / `sessionQuery` / `NavBar` / `App` suites.
- `npm run build` -- `tsc -b` + `vite build` succeed.
- `npm run generate:api && git status --porcelain src/shared/api` -- empty.

**Manual checks:**
- `docker compose up`; open `/sign-in`, toggle to **Job Seeker**: both options selectable, the name field reads "Full name", the "coming soon" note is gone. Register a Job Seeker with an email you already used for a Company → it succeeds; the nav shows the Job Seeker's name + Log out; reload keeps the session.
- Re-register the same Job Seeker email → "This email is already registered as a Job Seeker." under the email field.
- Sign in as that Job Seeker with a wrong password → "That email and password don't match. Please try again.".
- Fill the email, switch the toggle Company↔Job Seeker: the email value survives, any visible field error clears, and the name label tracks the role.
