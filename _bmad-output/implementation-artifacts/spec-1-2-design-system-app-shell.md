---
title: 'From-scratch design system and the role-aware application shell'
type: 'feature'
created: '2026-09-06'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'bba8fdf1d7ecfc22735ad61c19c689dfa3546eb6'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The frontend is a walking-skeleton placeholder: FSD layers exist as empty barrels, there is no visual system, no navigation shell, and no route surface. Every later story (auth in 1.3/1.4, postings in Epic 2) would otherwise invent its own styling and chrome.

**Approach:** Build the Nexus Indigo design-token system in `shared/`, a reusable `auth-role-toggle` and 1120px layout container in `shared/ui`, a role-aware application shell (nav bar) in `widgets/`, and the Home / Search landing route in `pages/`, wired together by a React Router setup in `app/`. Tokens are the single source of colour, type, radius, and spacing; nothing hardcodes those values outside the token layer.

## Boundaries & Constraints

**Always:**
- FSD downward-imports only (`app → pages → widgets → features → entities → shared`); `npm run lint` and `npm run test:fsd-gate` stay green.
- Every colour, font-size, radius, and spacing value comes from `shared/tokens`; components reference token CSS variables or the typed token module, never raw literals.
- Token values match DESIGN.md exactly: colours (`background #F5F6FB`, `surface #FFFFFF`, `primary #23215E`, `primary-foreground #FFFFFF`, `accent #0A7A90`, `accent-foreground #FFFFFF`, `text-primary #1A1B2E`, `text-secondary #5B5E78`, `border #DFE1F0`, `success #0B7A42`, `success-subtle #E3F6EA`, `danger #C42744`, `danger-subtle #FBE4E8`); Inter type ramp (display 40/700/-0.02em, heading 28/700/-0.01em, heading-sm 20/600, body 16/400, body-sm 14/400, label 13/600/+0.04em, caption 12/500); radius (sm 6, md 10, lg 16, xl 24, full 9999); spacing 4px base 1–8 (4/8/12/16/24/32/48/64), gutter 32, editorial-gap 96.
- Content sits in a fixed 1120px max-width container; light mode only; no responsive breakpoints.
- WCAG 2.1 AA behavioural floor: visible focus indicator on every interactive element via `:focus-visible`; focus order matches reading order; `auth-role-toggle` operable by `Tab` + arrow keys with `Enter`/`Space` to select; `@media (prefers-reduced-motion: reduce)` suppresses all fade/slide transitions.
- `accent` is used only for the single primary action per surface; `primary` is chrome/identity only.
- Microcopy is formal: complete sentences, terminal punctuation, no exclamation marks or emoji.

**Never:**
- No auth logic, no API calls, no `/api/auth/*` wiring, no `entities/*` or `features/*` slices with server state — that is stories 1.3/1.4. The shell renders the signed-out state only; it accepts a `viewer` value so 1.3 can supply a role without reshaping it.
- No Search behaviour — the landing search bar is visual only; the browse list shows the empty-catalog state. Real search is Epic 2.
- No dark mode, no theming switch, no responsive breakpoints, no component-library dependency (Material, Chakra, etc.).
- No backend changes; no change to the ESLint boundaries rule logic or `check-fsd-gate.mjs`.
- No nav item — enabled or disabled — pointing at a Company-only or Job-Seeker-only surface while the viewer is anonymous.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Anonymous visitor opens `/` | no session | Shell renders: `NexusJob` wordmark, `Search` link, `Sign up / Log in` link. Home renders the display headline "Find your next role. Post your next hire.", the sub-line, a visual search bar, the note "Browsing and searching do not require an account.", and the empty-catalog state "No open postings yet. Check back soon." | N/A |
| `auth-role-toggle` renders | no explicit selection | Two-option pill ("Company" / "Job Seeker"), exactly one active (default "Job Seeker"), active option uses `primary` fill + `full` radius; `role="radiogroup"`, options `role="radio"` with `aria-checked` | N/A |
| `auth-role-toggle` keyboard | focus in group; `ArrowRight`/`ArrowLeft`/`ArrowUp`/`ArrowDown` | Active option moves and roving `tabindex` follows; `Enter`/`Space` confirms; `onChange` fires with the new role; visible focus ring throughout | N/A |
| Reduced motion | OS `prefers-reduced-motion: reduce` | Nav and route-change fade/slide transitions render with no animation (instant) | N/A |
| Unknown client route | navigate to `/does-not-exist` | Catch-all route renders inside the shell: "This page is not available." with a link to Home | Renders the not-available surface, not a blank screen |
| Narrow viewport | window < 1120px | Container holds 1120px max-width; page scrolls; no breakpoint reflow, no layout break | N/A |

## Resolved Decisions

- **Inter is self-hosted** via `@fontsource-variable/inter`, imported in `app/App.tsx` and bundled by Vite. No `<link>` to Google Fonts; no request to `fonts.googleapis.com` / `fonts.gstatic.com`. The DESIGN.md fallback stack stays in the `--font-family` token as the fallback.
- **A frontend unit-test runner is added in this story**: Vitest + React Testing Library + `@testing-library/user-event` + jsdom. `npm test` runs in the CI `frontend` job. It covers `RoleToggle` (default active option, arrow-key navigation, `Enter`/`Space` selection, ARIA state) and `NavBar` (anonymous link set has no role-restricted target).
- **The token constraint is a CI gate**: Stylelint with a disallowed-list rejecting raw hex colours and raw `px` in `font-size` / `border-radius` anywhere under `src/` except `shared/tokens/`. `npm run lint:tokens` runs in the CI `frontend` job. Runs against `.css` files; token references in `.tsx` inline styles must come from `tokens.ts`.
- **Scope is one story** — the token system and the shell ship together in this spec, as the epic frames them.

</frozen-after-approval>

## Code Map

- `frontend/src/shared/index.ts` -- currently exports `APP_NAME`; extend to re-export the token module and `shared/ui`. Keep `APP_NAME`.
- `frontend/src/shared/tokens/` -- NEW. `tokens.css` (`:root` custom properties `--color-*`, `--font-*` / `--text-*`, `--radius-*`, `--space-*`, `--gap-*`), `base.css` (body background + base type, `*:focus-visible` outline, reduced-motion block, `#root` reset), `tokens.ts` (typed constants + type ramp helper for JS/inline-style use), `index.ts`.
- `frontend/src/shared/ui/` -- NEW. `Container.tsx` (1120px max-width, centered, `gutter` side padding), `RoleToggle.tsx` (the `auth-role-toggle`: radiogroup, roving tabindex, arrow-key nav), `index.ts`.
- `frontend/src/widgets/app-shell/` -- NEW. `AppShell.tsx` (nav bar + `<Outlet/>` in a `Container`), `NavBar.tsx` (wordmark + role-aware links; anonymous set = Search, Sign up / Log in), `index.ts`. `viewer` prop typed `{ kind: 'anonymous' }` now, extensible to `'company' | 'jobSeeker'`.
- `frontend/src/pages/home/` -- NEW. `HomePage.tsx` (hero headline/sub-line, visual `SearchBar`, hero note, empty-catalog state), `index.ts`.
- `frontend/src/pages/not-found/` -- NEW. `NotFoundPage.tsx` ("This page is not available." + Home link), `index.ts`.
- `frontend/src/app/App.tsx` -- REPLACE placeholder: import `@fontsource-variable/inter`, `shared/tokens` CSS, build the router (`createBrowserRouter`: `/` → `AppShell` layout → `HomePage`; `*` → `NotFoundPage` in the shell), render `<RouterProvider>`.
- `frontend/src/app/index.ts` -- unchanged (still `export { App }`).
- `frontend/package.json` -- add deps `react-router` and `@fontsource-variable/inter`; devDeps `vitest`, `@vitejs/plugin-react` (present), `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `jsdom`, `stylelint`, `stylelint-config-standard`; scripts `"test": "vitest"`, `"lint:tokens": "stylelint \"src/**/*.css\""`. Pin exact versions in `package-lock.json` via `npm install`.
- `frontend/vite.config.ts` -- add the Vitest `test` block (`environment: 'jsdom'`, `setupFiles`, `globals: true`); reference via `/// <reference types="vitest/config" />`.
- `frontend/vitest.setup.ts` -- NEW. `import '@testing-library/jest-dom/vitest'`.
- `frontend/.stylelintrc.json` -- NEW. `extends stylelint-config-standard`; add `color-no-hex` and `declaration-property-value-disallowed-list` for `/px/` on `font-size` / `border-radius`; `ignoreFiles: ["src/shared/tokens/**"]`.
- `frontend/tsconfig.node.json` -- add `vitest.config` / setup file to `include` if needed for type-checking.
- `frontend/index.html` -- add `<meta name="color-scheme" content="light">`.
- `.github/workflows/ci.yml` -- `frontend` job: add steps `npm run lint:tokens` and `npm test -- --run` (after the existing lint step, before build). No other job changes.
- `frontend/eslint.config.js`, `frontend/scripts/check-fsd-gate.mjs` -- read-only reference; do not modify.
- `_bmad-output/implementation-artifacts/spec-1-1-walking-skeleton.md` -- continuity: FSD barrel pattern, boundaries config golden shape (Design Notes), `APP_NAME` in `shared`.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/shared/tokens/tokens.css` -- define every DESIGN.md colour, type-ramp, radius, and spacing value as `:root` CSS custom properties -- single source of visual truth.
- [x] `frontend/src/shared/tokens/base.css` -- body background/base type from tokens, `*:focus-visible` outline, `@media (prefers-reduced-motion: reduce)` transition kill-switch, `#root` reset -- a11y + light-mode floor.
- [x] `frontend/src/shared/tokens/tokens.ts` + `index.ts` -- typed token constants and a type-ramp helper for inline-style use; barrel export -- lets TSX reference tokens without raw literals.
- [x] `frontend/src/shared/ui/Container.tsx` + `index.ts` -- 1120px max-width centered wrapper with `gutter` side padding -- the layout constraint, reused by every surface.
- [x] `frontend/src/shared/ui/RoleToggle.tsx` -- `auth-role-toggle`: `role="radiogroup"`, two `role="radio"` options, roving `tabindex`, arrow-key + `Enter`/`Space` selection, `onChange(role)`, active = `primary` fill + `full` radius -- reused by the 1.3/1.4 auth surface and the apply-gate.
- [x] `frontend/src/shared/index.ts` -- re-export `./tokens` and `./ui`; keep `APP_NAME` -- one import surface for the layer.
- [x] `frontend/src/widgets/app-shell/NavBar.tsx` -- `primary` bar, wordmark, role-aware link set (anonymous: Search, Sign up / Log in), no dead/disabled items -- the shared chrome.
- [x] `frontend/src/widgets/app-shell/AppShell.tsx` + `index.ts` -- NavBar + `<Outlet/>` inside `Container`; `viewer` prop -- the layout route element.
- [x] `frontend/src/pages/home/HomePage.tsx` + `index.ts` -- display headline, sub-line, visual `SearchBar` (no submit behaviour), hero note, empty-catalog state -- the landing surface.
- [x] `frontend/src/pages/not-found/NotFoundPage.tsx` + `index.ts` -- "This page is not available." + Home link -- catch-all, no blank screen.
- [x] `frontend/src/app/App.tsx` -- import font + token CSS, build `createBrowserRouter` (`/` layout → Home; `*` → NotFound), render `<RouterProvider>` -- wires the shell to routes.
- [x] `frontend/package.json` + `package-lock.json` -- add `react-router`, `@fontsource-variable/inter`, Vitest + Testing Library + jsdom, Stylelint; add `test` and `lint:tokens` scripts.
- [x] `frontend/vite.config.ts` + `frontend/vitest.setup.ts` -- Vitest config (jsdom, globals, setup) and jest-dom setup.
- [x] `frontend/.stylelintrc.json` -- standard config + no-raw-hex / no-raw-px rules, ignoring `src/shared/tokens/**`.
- [x] `.github/workflows/ci.yml` -- add `npm run lint:tokens` and `npm test -- --run` steps to the `frontend` job.
- [x] `frontend/index.html` -- add `color-scheme` meta.
- [x] `frontend/src/shared/ui/RoleToggle.test.tsx` -- default active option is "Job Seeker"; `ArrowRight`/`ArrowLeft` move the active option; `Enter`/`Space` fire `onChange`; `role="radiogroup"` + `aria-checked` reflect state.
- [x] `frontend/src/widgets/app-shell/NavBar.test.tsx` -- with `viewer.kind === 'anonymous'`, rendered links are exactly Search + Sign up / Log in; no link `href` targets a Company/Job-Seeker-only route.

**Acceptance Criteria:**
- Given the built app, when an anonymous visitor loads `/`, then the shell and Home surface render exactly the copy and controls in the I/O matrix, and the DOM contains no nav item pointing at a Company-only or Job-Seeker-only route.
- Given any `src/**/*.css` outside `shared/tokens/`, when `npm run lint:tokens` runs, then a raw hex colour or raw `px` `font-size` / `border-radius` fails CI.
- Given `npm run build`, when it completes, then `tsc -b` passes, `vite build` succeeds, and the served fonts produce no request to `fonts.googleapis.com` / `fonts.gstatic.com`.
- Given the CI `frontend` job, when it runs on this branch, then lint, fsd-gate, build, and every added step pass.
- Given the `auth-role-toggle` component under test, when arrow keys then `Enter`/`Space` are pressed, then the active role changes and `onChange` fires; `base.css` applies a `:focus-visible` outline to every interactive element. (Story 1.2 ships and unit-tests the component; story 1.3 mounts it in the Sign up / Log in surface — see Design Notes, `/sign-in` seam.)
- Given `prefers-reduced-motion: reduce`, when a route change or nav hover occurs, then no fade/slide animation plays.

**Pass 1 review patches (added 2026-09-06, all applied):**
- [x] `frontend/scripts/check-tokens-gate.mjs` + `.github/workflows/ci.yml` -- fixture-based negative test for the token gate (raw hex + raw `px` `font-size`/`border-radius` must make stylelint exit non-zero), mirroring `check-fsd-gate.mjs`; wire a CI step. Add a `test:tokens-gate` script.
- [x] `.github/workflows/ci.yml` -- after Build, a step that greps `frontend/dist` and fails on any `fonts.googleapis.com` / `fonts.gstatic.com` reference (enforces the no-Google-Fonts AC).
- [x] `frontend/test/tokens-parity.test.ts` + `frontend/src/shared/tokens/tokens.ts` -- assert `tokens.ts` values equal the `tokens.css` custom properties (case-insensitive for hex); normalise the TS hex literals to lowercase to match. (28 parameterised cases.)
- [x] `frontend/src/shared/ui/RoleToggle.test.tsx` -- add one assertion for the uncontrolled `defaultValue` prop path.
- [x] `frontend/src/app/App.tsx` -- add a router `errorElement` (`RouteError`) that renders `role="alert"` copy inside a `Container`.
- [x] `frontend/test/design-invariants.test.ts` -- derive `srcRoot` from `import.meta.dirname`, not `process.cwd()`.
- [x] `frontend/.stylelintrc.json` + `frontend/test/design-invariants.test.ts` -- `media-feature-name-disallowed-list` now `["width","min-width","max-width"]` + `media-feature-range-notation: "prefix"`; test regex catches `(min-width`, `(max-width`, `(width >=`, `(width <=`, `(<n> <= width`.
- [x] `frontend/src/shared/tokens/base.css` -- deleted `border-radius: var(--radius-sm)` from `*:focus-visible`.
- [x] `frontend/src/shared/tokens/base.css` -- `#root { min-height: 100dvh }` (removed `#root` from the `html, body` `min-height: 100%` selector).

## Implementation Notes

- **Resolved dep versions** (pinned in `package-lock.json`): `react-router@8.3.1`, `@fontsource-variable/inter@5.3.0` (self-hosted family name `Inter Variable`), `vitest@5.0.0`, `@testing-library/react@16`, `@testing-library/user-event@14`, `@testing-library/jest-dom@7`, `jsdom@30`, `stylelint@17` + `stylelint-config-standard@40`.
- **Focus ring colour is `--color-primary`, not `--color-accent`.** `accent` is reserved for the single primary action per surface, so the global `*:focus-visible` outline uses `primary`; the `NavBar` (painted with `primary`) overrides the ring locally to `--color-primary-foreground` so it stays visible on the dark bar.
- **`RoleToggle` uses selection-follows-focus** (WAI-ARIA radio-group pattern): arrow keys move the active option, update `aria-checked`, move roving `tabindex`, and fire `onChange`; `Enter`/`Space` re-confirm the focused option and fire `onChange` again. Controlled (`value`) and uncontrolled (`defaultValue`, default `jobSeeker`) modes both supported.
- **Type ramp exists in two forms:** per-step CSS sub-tokens (`--text-<step>-size|weight|line|tracking`) consumed by component CSS modules, and `typeRamp` / `typeStyle()` in `tokens.ts` for inline-style use. No component uses raw literals; all component CSS is CSS Modules referencing `var(--…)`.
- **Stylelint deviation from spec:** `.stylelintrc.json` also nulls `selector-class-pattern` (CSS-Modules classes), `no-descending-specificity`, and three `*-empty-line-before` rules — pure style-policing that the spec's Design Notes explicitly permit dropping. `color-no-hex` + the `font-size`/`border-radius` `px` disallowed-list (the token gate) are kept and verified working.
- **`vitest.setup.ts` added to `tsconfig.app.json` `include`** (not `tsconfig.node.json` as the Code Map suggested): the jest-dom matcher augmentation needs the DOM lib, which only the app project has.
- **`Sign up / Log in` nav link points at `/sign-in`**, which has no route yet, so it currently resolves to the `NotFoundPage`. Story 1.3 owns that surface and route; the link and its target path are the seam.
- **Matrix-audit follow-up (post-implementation, by the orchestrator):** the subagent's 8 tests left I/O-matrix rows 1 (anonymous opens `/`), 4 (reduced motion), 5 (unknown route) and 6 (narrow viewport) without covering tests. Added:
  - `frontend/src/app/App.tsx` now exports its `routes` array so a test can mount the tree with an in-memory router.
  - `frontend/src/app/App.test.tsx` — renders the real route tree via `createMemoryRouter` at `/` (asserts shell + full Home copy) and at `/does-not-exist` (asserts shell + not-available copy + Home link). Covers rows 1 and 5.
  - `frontend/test/design-invariants.test.ts` (under the node tsconfig, added to `tsconfig.node.json` `include`) — source guards that `base.css` keeps the `prefers-reduced-motion: reduce` transition/animation kill-switch and the `:focus-visible` outline (row 4), that `Container.module.css` caps at `1120px`, and that no `src/**/*.css` introduces a `(max|min)-width` breakpoint (row 6). jsdom cannot evaluate media queries or layout, so these assert the mechanism at source level.
  - `.stylelintrc.json` — added `media-feature-name-disallowed-list: ["max-width", "min-width"]` so "no responsive breakpoints" is also a live `npm run lint:tokens` gate.
- **Verification (branch `feat/epic-1-story-2-design-system-app-shell`):** `npm ci`, `npm run lint`, `npm run test:fsd-gate`, `npm run lint:tokens`, `npm test -- --run` (**4 files, 14 tests passing**), `npm run build` (`tsc -b` over app + node projects, then `vite build`) all green; `dist` bundles 7 self-hosted Inter `woff2` files and `grep` of `dist/` finds no `fonts.googleapis.com` / `fonts.gstatic.com` reference; built CSS contains the token custom properties, `Inter Variable`, the `prefers-reduced-motion` block, and `focus-visible`.

## Spec Change Log

### Pass 1 review (2026-09-06)

- **Trigger:** review flagged that the `auth-role-toggle` / Sign up-Log in surface has no home in the running 1.2 app, and that AC #5 was worded as an in-app keyboard flow that 1.2 does not deliver.
- **Amended (non-frozen only):** AC #5 reworded to the component / unit-test level. Added a Design Note recording the `/sign-in` seam as an accepted interim state — **human decision (review pass 1): keep the graceful in-shell not-found until story 1.3**, do not build a sign-in page or hide the nav link in 1.2. Added nine review patches to Tasks.
- **Known-bad avoided:** an approved AC that cannot be demonstrated in the shipped app; a token-SSOT gate with no proof it fires; an explicit AC (no Google Fonts) with no CI enforcement; a router with no in-shell error surface.
- **KEEP (must survive re-derivation):** the working shell, tokens, `shared/ui`, pages, and router are correct — the nine patches are *additive*, do not revert or reshape them. Preserve: the `viewer` seam; self-hosted Inter; CSS-Modules-with-`var(--…)` in every component; `RoleToggle` in `shared/ui`; the FSD / token / I-O-matrix test coverage already added (`RoleToggle.test.tsx`, `NavBar.test.tsx`, `App.test.tsx`, `test/design-invariants.test.ts`).

## Review Triage Log

### Pass 1 (2026-09-06) — blind-hunter, edge-case-hunter, verification-gap

**Outcome:** one bad_spec group (resolved by the human, documentation-only — see Spec Change Log), nine `patch` entries (all applied by the step-03 subagent and re-verified: `tsc -b`, `eslint`, `lint:tokens`, `test:fsd-gate`, `test:tokens-gate`, `vitest run` = 5 files / 43 tests, `build`, dist no-Google-Fonts grep — all green). The nine patches are small, additive, and independently verified; the orchestrator self-reviewed each patched file rather than re-running the three-layer review on the small delta at iteration 1/5.

**Loopback group — bad_spec (medium), RESOLVED by human (keep-as-is): the auth-role-toggle / Sign up-Log in surface has no home in the running 1.2 app.**
Resolution (review pass 1): human chose to keep the graceful in-shell not-found for `/sign-in` until story 1.3. Non-frozen AC #5 reworded to component level; Design Note added; no code change for this group. Fix was documentation-only — no code revert.
- `blind-hunter` — the primary anonymous CTA "Sign up / Log in" targets `/sign-in`, which has no route, so it renders `NotFoundPage`. Verified: `navItemsFor({kind:'anonymous'})` returns `{to:'/sign-in'}`; routes are `/`→Home, `*`→NotFound. Users do meet this (it is the main call-to-action).
- `edge-case-hunter` (claim) — frozen AC "Given keyboard-only operation, when a user tabs to the `auth-role-toggle` …" is unexercisable: `RoleToggle` is imported only by its own test; no route element or surface mounts it. Verified by grep — zero non-test consumers.
- Shared root cause: the frozen matrix shows the "Sign up / Log in" affordance and has a matrix row for the toggle "when it is rendered", plus an AC for using it in-app, but the scope (Never: auth surface is 1.3/1.4; Tasks: only `home` + `not-found` pages) builds no screen that hosts it. Whether 1.2 should (a) accept a graceful in-shell not-found until 1.3, (b) ship a minimal static `pages/sign-in` surface now, or (c) omit the nav link until 1.3 is a scope decision the frozen intent does not settle. → routed to the human.

**patch — carried into the loopback (fold into the re-plan / re-implementation):**
- `verification-gap` (pre-verified) — the design-token CI gate (`lint:tokens`) ships with no negative test proving it rejects a raw hex / raw `px` value, unlike the sibling FSD gate which has `check-fsd-gate.mjs`. medium (the token-SSOT deliverable rests on a gate that could silently no-op on a stylelint major bump). Fix: add `frontend/scripts/check-tokens-gate.mjs` mirroring `check-fsd-gate.mjs` + a CI step.
- `blind-hunter` — AC "no request to `fonts.googleapis.com` / `fonts.gstatic.com`" has no CI/test enforcement, only a manual grep. low. Fix: a CI step that greps `dist/` and fails on a match.
- `blind-hunter` + `verification-gap` — `tokens.ts` hand-mirrors every `tokens.css` value with no parity test, and the two disagree on hex case (`#23215E` vs `#23215e`). low now (no consumer), medium risk once story 1.3 imports `tokens.ts`. Fix: a parity test asserting `tokens.ts` ≡ `tokens.css` (case-insensitive) + normalise the TS hex case.
- `blind-hunter` — `RoleToggle`'s shipped `defaultValue` prop has no test (only the implicit default and controlled `value` are covered). low. Fix: one assertion in `RoleToggle.test.tsx`.
- `edge-case-hunter` — the router has no `errorElement`; a render throw in `AppShell`/`HomePage`/`NotFoundPage` shows React Router's default screen outside the shell. low (static components, no loaders). Fix: an `errorElement` rendering a message inside a `Container`.
- `edge-case-hunter` — `design-invariants.test.ts` builds `srcRoot` from `process.cwd()`; breaks if vitest runs from a directory other than `frontend/`. low. Fix: derive the path from `import.meta.dirname`.
- `edge-case-hunter` — the no-breakpoints gate (stylelint `media-feature-name-disallowed-list` + the source regex) misses CSS range syntax `@media (width >= 1120px)`. low. Fix: add `width` to the disallowed list and broaden the regex.
- `blind-hunter` — `*:focus-visible` sets `border-radius: var(--radius-sm)`, which rounds the *box* of every focused element (a pill-radius button snaps to 6px while focused) and does not affect the outline. low, cosmetic. Fix: delete that one declaration.
- `blind-hunter` — `base.css` sets `min-height: 100%` on `html, body, #root` with no `html { height: 100% }`, so `#root` does not fill the viewport on short pages (possible background strip below the content on the landing page). low, cosmetic. Fix: `min-height: 100dvh` on `#root`.

**Rejected:**
- `blind-hunter` — "line-height values invented, no basis in DESIGN.md": **false**. DESIGN.md §typography defines `lineHeight` for every step (display 1.15, heading 1.2, heading-sm 1.3, body 1.6, body-sm 1.55, label 1.4, caption 1.4); `tokens.css` matches exactly. The reviewer was context-free without DESIGN.md.
- `blind-hunter` — "status metadata disagrees / baseline_commit matches no commit / branch mismatch": **false**. `in-review` is the correct step-4 state; `bba8fdf` is the real HEAD of the story-1.1 work this branched from; the branch is `feat/epic-1-story-2-…`; the empty Change Log / Triage Log are populated by this very pass.
- `blind-hunter` — "route-change fade/slide transitions specified but never built": low, rejected. The transitions that exist (nav-link `opacity`, toggle `background-color`) are covered by the reduced-motion block; route-change transitions were never an AC or a "Never", and the only fixes are scope creep or a frozen-spec edit.
- `blind-hunter` / `edge-case-hunter` — "`RoleToggle` roving-tabindex / bad `value` prop desyncs focus": low, rejected. Reachable only via a broken controlled usage (parent ignores `onChange`) or an out-of-union `value` that TypeScript forbids; correct controlled use re-renders the roving `tabindex` onto the focused node.
- `blind-hunter` — "`onChange` fires on `Enter`/`Space` with an unchanged value": low, rejected. Deliberate WAI-ARIA selection-follows-focus + confirm behaviour, asserted on purpose in the test; a consumer can ignore a same-value call.
- `blind-hunter` — "border `1px` and `max-width: 720/640/480px` literals bypass the token gate": low, rejected. DESIGN.md itself defines neither a border-width token nor a prose-measure scale; inventing both exceeds the spec and DESIGN.
- `blind-hunter` — "inline-style literals unenforced by `lint:tokens`": low, rejected. Every component uses CSS Modules with `var(--…)`; enforcing inline styles needs a bespoke ESLint rule for a pattern nothing in the tree uses.
- `blind-hunter` — "Home sub-line / search placeholder copy not in frozen spec": low, rejected. The matrix left the sub-line unquoted (implementer latitude); the shipped copy is formal, complete-sentence, no exclamation/emoji — compliant with the microcopy constraint.
- `blind-hunter` — "'Search' nav item and the wordmark both target `/`": low, rejected. Matches the EXPERIENCE IA ("Home / Search landing | App load (root URL)") and the `key-home-search` mockup, which shows both.
- `blind-hunter` — "`NavBar.test` hard-codes the role-restricted route list": low, rejected. Test-only readability nit; no shared constant exists to reference yet.
- `blind-hunter` — "test and production tsconfig not separated": low, rejected. Standard Vite `react-ts` layout; `tsc -b` type-checking test files with devDeps present is harmless, and `-- --run` in CI is a one-token idiom.
- `edge-case-hunter` — "token gate misses `rgb()` / named colours / `rem` / `font` shorthand / `border-*-radius` longhand": low, rejected. A documented best-effort backstop (same framing as story 1.1's raw-SQL scanner); `color-no-hex` + the `px` list cover the common cases, and the comprehensive rule set risks false positives.
- `edge-case-hunter` (deletion, low confidence) — "`APP_NAME` export now has no importer": low, rejected. Harmless unused export; likely wanted for page titles later.
- `verification-gap` (other) — "`design-invariants.test.ts` guards by source regex, not behaviour": low, rejected. jsdom cannot evaluate media queries or layout; the regex guards catch the likely regression (deletion) and the spec routes correctness checks to the manual list.
- `verification-gap` (other) — "`<App/>` component never rendered by a test": low, rejected. `App()` is a one-line `RouterProvider` pass-through and `createBrowserRouter(routes)` is already executed on import by `App.test.tsx`.

## Design Notes

- **`auth-role-toggle` lives in `shared/ui`, not `features/`.** It is purely presentational — a controlled two-option radiogroup with no domain knowledge, no server state, no auth logic. Story 1.3 composes it into the `features/*` auth surface and supplies the `onChange` handler. Placing it in `shared/ui` keeps it reusable by both the standalone auth surface and the apply-gate modal (Epic 3) without an upward import.
- **Tokens are CSS custom properties first, TS second.** `tokens.css` on `:root` is the canonical set; `tokens.ts` mirrors the values for the few places that need them in JS (inline styles, tests). Components should prefer `var(--…)` in CSS/`style` and import from `tokens.ts` only when a computed value is unavoidable.
- **Router shape.** One layout route (`AppShell`) owns the chrome; child routes render into its `<Outlet/>`. `/` → `HomePage`; `*` → `NotFoundPage` (still inside the shell). The Host already serves `index.html` for non-`/api` paths (story 1.1), so `createBrowserRouter` client-side paths resolve on hard refresh.
- **`viewer` seam.** `AppShell` / `NavBar` take `viewer: { kind: 'anonymous' }` now. Story 1.3 widens the union to `{ kind: 'company' | 'jobSeeker'; displayName: string }` and feeds it from `GET /api/auth/me`; the nav-item selection is already a function of `viewer.kind`, so no reshaping.
- **The `/sign-in` seam.** The anonymous nav's "Sign up / Log in" item targets `/sign-in`, which has no route in 1.2 and renders `NotFoundPage` ("This page is not available.") inside the shell. Story 1.3 builds that route and mounts the `RoleToggle` in the auth surface. Accepted interim state (human decision, review pass 1): the "no dead nav item" rule is scoped to role-restricted surfaces; a graceful in-shell not-found for an open route arriving in the next story — never a blank screen — is acceptable. `RoleToggle` therefore has no in-app consumer in 1.2 and is covered by `RoleToggle.test.tsx` only.
- Type-ramp reference (DESIGN.md): `label` = 13px/600, `letter-spacing: 0.04em` — used for nav links, button labels, and the toggle's two options.
- **Stylelint scope is narrow on purpose.** The gate exists for the token constraint, not general CSS style policing. If `stylelint-config-standard` is too noisy, keep only what's needed for `color-no-hex` + the `px` disallowed-list and drop the rest — the FSD/token boundary is the deliverable, not CSS bikeshedding.
- **`react-router` version.** Architecture names "v8"; take whatever `npm install react-router` resolves to at build time and let `package-lock.json` pin it. Use the data-router API (`createBrowserRouter` + `RouterProvider`).

## Verification

**Commands:**
- `cd frontend && npm ci` -- expected: clean install against the updated lockfile.
- `npm run lint` -- expected: 0 errors (FSD boundaries clean).
- `npm run test:fsd-gate` -- expected: gate still rejects an upward import.
- `npm run lint:tokens` -- expected: pass (Stylelint finds no raw hex / raw px outside `shared/tokens/`).
- `npm test -- --run` -- expected: all `RoleToggle` / `NavBar` specs pass.
- `npm run build` -- expected: `tsc -b` + `vite build` succeed; `dist/assets` contains Inter `woff2` and no Google Fonts URL appears anywhere in `dist`.

**Manual checks:**
- `npm run preview`, load `/`: confirm the wordmark, the two anonymous nav links, the display headline, the visual search bar, the hero note, and the empty-catalog copy; confirm no Company/Job-Seeker-only nav item is present.
- Keyboard: `Tab` through nav → search bar → (on the 1.3 surface later) the toggle; confirm a visible focus ring on each; on `RoleToggle` (render it on a scratch route or in a test), arrow keys move the active option and `Enter`/`Space` selects.
- OS reduced-motion on: reload and navigate; confirm no fade/slide.
- DevTools Network: confirm no request to `fonts.googleapis.com` or `fonts.gstatic.com`.
- Resize below 1120px: confirm the container keeps its width and the page scrolls rather than reflowing.
