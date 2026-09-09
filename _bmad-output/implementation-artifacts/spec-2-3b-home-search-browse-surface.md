---
title: 'Home search / browse surface'
type: 'feature'
created: '2026-09-10'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '434e7b9171c19ac3237529ed31c482a00155423f'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-NexusJobBmad-2026-09-05/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 2.3a's `GET /api/job-postings` search endpoint exists but nothing calls it — the Home page's search bar is still visual-only and the browse list is a hardcoded empty-state string (story 1.2 placeholder).

**Approach:** Wire `HomePage` to an explicit-submit search form driving a new `useJobPostingSearch` query hook against 2.3a's endpoint, rendering results as a stack of whole-card links (`JobPostingCard`) with a `Pagination` control. The page always runs a search — starting with an empty keyword (browse-all) on load, per 2.3a's "missing/empty query = browse everything" semantics — so cards render immediately, before any submit.

## Boundaries & Constraints

**Always:**
- `HomePage` holds three pieces of local state: the live `keyword` input value (uncontrolled by search — no as-you-type), `submittedQuery` (defaults to `''`), and `page` (defaults to `1`). Only form submit updates `submittedQuery` (trimmed) and resets `page` to `1`; typing alone never triggers a search.
- `useJobPostingSearch(submittedQuery, page, PAGE_SIZE)` runs unconditionally (no `enabled` gate) with a fixed `PAGE_SIZE = 20` (matches 2.3a's server default) — this is what makes the initial `submittedQuery === ''` load a real browse-all.
- Render branches, in this precedence: `isPending` -> skeleton rows; `isError` -> error banner (`role="alert"`) + a retry button that calls the query's `refetch()` (same keyword, same page); `isSuccess && data.total === 0 && submittedQuery === ''` -> empty-catalog copy ("No open postings yet. Check back soon.") — this is the true site-wide-empty case, never conflated with an empty search that simply hasn't run yet; `isSuccess && data.total === 0 && submittedQuery !== ''` -> no-match copy (`No postings match "{submittedQuery}." Try a different term.`); `isSuccess && data.total > 0` -> the card stack + `Pagination`, rendered even when there is only one page (both buttons disabled).
- `JobPostingCard` is a single whole-card `<Link to={`/job-postings/${id}`}>` (no nested interactive elements) showing title, company name, description; hover elevation only (DESIGN.md `job-card`: resting = border only, hover = `0 1px 2px rgba(26,27,46,0.06), 0 4px 12px rgba(26,27,46,0.06)`); no Apply control (Epic 3 scope).
- `Pagination` is generic (`shared/ui`): `page`, `pageSize`, `total`, `onPageChange`; computes `totalPages = Math.max(1, Math.ceil(total / pageSize))`; Prev disabled at `page <= 1`, Next disabled at `page >= totalPages`; renders "Page {page} of {totalPages}" between them.

**Never:**
- No new route — Home (`/`) is both landing and results; no URL-driven search state (no query params, no history entries per keystroke or page change).
- No shared `shared/ui/Skeleton` primitive — `JobPostingCardSkeleton` stays local to `entities/job-posting/ui`, mirroring `PostingDetailPage`'s own inlined skeleton rather than extracting a premature abstraction.
- No debounce, no as-you-type filtering, no client-side re-sorting or re-filtering of the returned page.

</frozen-after-approval>

## Code Map

**Entities — the search query**
- `frontend/src/entities/job-posting/api/jobPostingsClient.ts` -- add `search: (query: string, page: number, pageSize: number): Promise<PageOfJobPostingSearchResultResponse> => rawClient.search(query, page, pageSize)`. The generated `JobPostingsClient.search`'s params are typed `any` (NSwag couldn't infer `integer` from the OpenAPI schema — a known, deferred, pre-existing gap; see `deferred-work.md`); this wrapper's own signature is `number`, so every caller in the app gets real typing without touching the generated file.
- `frontend/src/entities/job-posting/model/jobPostingQuery.ts` -- add `export const jobPostingSearchQueryKey = (query: string, page: number, pageSize: number) => ['job-postings', 'search', query, page, pageSize] as const` and `export function useJobPostingSearch(query: string, page: number, pageSize: number) { return useQuery({ queryKey: jobPostingSearchQueryKey(query, page, pageSize), queryFn: () => jobPostingsClient.search(query, page, pageSize) }) }`. No `staleTime: Infinity` (unlike `useJobPosting`) — new postings can appear between searches, so each distinct key refetches fresh.
- `frontend/src/entities/job-posting/index.ts` + `frontend/src/entities/index.ts` -- add `jobPostingSearchQueryKey`, `useJobPostingSearch` to the barrels (mirrors how `useJobPosting` was added in 2.2b).

**Entities — the card**
- `frontend/src/entities/job-posting/ui/JobPostingCard.tsx` -- NEW. Props `{ id: string; title: string; companyName: string; description: string }`. `<Link to={`/job-postings/${id}`} className={styles.card}>` containing `<h3>{title}</h3>` (`heading-sm`), `<p>{companyName}</p>` (`body-sm` / `text-secondary`), `<p>{description}</p>` (`body`, truncate via CSS `line-clamp`, not string slicing).
- `frontend/src/entities/job-posting/ui/JobPostingCard.module.css` -- NEW, tokens only. `.card`: `--color-surface`, `--radius-lg`, `1px solid --color-border`, `--space-5` padding, `display: block`, `text-decoration: none`, `color: inherit`; `:hover` adds the DESIGN.md two-layer `box-shadow` (no border/color change); `:focus-visible` keeps the global outline (`base.css`). Title/company/description rules per the type-ramp tokens above.
- `frontend/src/entities/job-posting/ui/JobPostingCardSkeleton.tsx` -- NEW. Same `.card` shell (no `Link`, `aria-hidden="true"`), three `--color-border` shimmer blocks (title-width, company-width, two description-width lines) — mirrors `PostingDetailPage.module.css`'s `.skeletonLine` pulse, including its `prefers-reduced-motion` override.
- `frontend/src/entities/job-posting/ui/index.ts` + `frontend/src/entities/job-posting/index.ts` + `frontend/src/entities/index.ts` -- barrel exports for both components.

**Shared — the pagination control**
- `frontend/src/shared/ui/Pagination.tsx` -- NEW. Props `{ page: number; pageSize: number; total: number; onPageChange: (page: number) => void }`. Two `<button type="button">` (Prev/Next, disabled per Boundaries), a `<span>Page {page} of {totalPages}</span>` between them; the whole control wrapped in `<nav aria-label="Search results pages">`.
- `frontend/src/shared/ui/Pagination.module.css` -- NEW, tokens only: flex row, `--space-2` gaps, buttons styled like `HomePage.module.css`'s existing `.search-button` but `--color-surface` background / `--color-border` outline (secondary, not the accent primary-action color — Pagination is navigation, not the surface's primary action) with a `:disabled` opacity reduction.
- `frontend/src/shared/ui/index.ts` -- add `export { Pagination } from './Pagination'`.

**Pages — the rewritten Home surface**
- `frontend/src/pages/home/HomePage.tsx` -- REWRITE. Local `useState` for `keyword`, `submittedQuery`, `page` (all per Boundaries). `<form role="search" onSubmit={handleSubmit}>` replaces the current plain `<div role="search">`; the button becomes `type="submit"`. `handleSubmit` calls `event.preventDefault()`, `setSubmittedQuery(keyword.trim())`, `setPage(1)`. Below the hero, the five render branches from Boundaries, keyed off `useJobPostingSearch(submittedQuery, page, PAGE_SIZE)`.
- `frontend/src/pages/home/HomePage.module.css` -- extend: keep `.page`/`.hero`/`.headline`/`.subline`/`.search`/`.search-input`/`.hero-note` as-is; change `.search` from a bare flex row to target `form.search` (same rules); add `.results` (flex column, `--space-4` gap, replaces `.catalog`'s box-panel-for-empty-state-only role — the panel styling (`.catalog`) is now only for the empty-catalog/no-match/error text states, not the card stack); keep `.empty-state` for empty-catalog/no-match copy; add `.error-banner` (mirrors `CreatePostingForm.module.css`'s `.banner` — danger token, `role="alert"`) and `.retry-button`.

**Tests**
- `frontend/src/entities/job-posting/api/jobPostingsClient.test.ts` -- add: `search` issues `GET` with the three params and resolves the `PageOfJobPostingSearchResultResponse`.
- `frontend/src/entities/job-posting/model/jobPostingQuery.test.tsx` -- add, mirroring `jobPostingQuery.test.tsx`'s existing `useJobPosting` cases: resolve -> `isSuccess` + `data`; reject -> `isError`.
- `frontend/src/entities/job-posting/ui/JobPostingCard.test.tsx` -- NEW: renders title/company/description; the whole card is one `<a>` with the correct `href`; no Apply button/link present.
- `frontend/src/entities/job-posting/ui/JobPostingCardSkeleton.test.tsx` -- NEW: renders with `aria-hidden="true"`, no text content.
- `frontend/src/shared/ui/Pagination.test.tsx` -- NEW: Prev disabled at `page=1`; Next disabled at the last page; clicking either (when enabled) calls `onPageChange` with the adjacent page number; "Page X of Y" text is correct.
- `frontend/src/pages/home/HomePage.test.tsx` -- NEW, mirroring `PostingDetailPage.test.tsx`'s mocked-client + `createMemoryRouter` harness: pending -> skeleton cards; success with results -> cards + Pagination, clicking Next calls `search` again with `page: 2`; success with `total: 0` and no submit yet -> empty-catalog copy; submitting a keyword that matches nothing -> no-match copy (with the keyword interpolated); a rejected query -> the error banner, and clicking retry calls `search` again with the same args; typing without submitting never calls `search` again.

## Tasks & Acceptance

**Execution:**
- [x] `frontend/src/entities/job-posting/api/jobPostingsClient.ts` -- `search` wrapper (typed `number`, not the generated `any`).
- [x] `frontend/src/entities/job-posting/model/jobPostingQuery.ts` -- `jobPostingSearchQueryKey` / `useJobPostingSearch`.
- [x] `frontend/src/entities/job-posting/ui/{JobPostingCard,JobPostingCardSkeleton}.tsx` + `.module.css` + barrels.
- [x] `frontend/src/shared/ui/Pagination.tsx` + `.module.css` + barrel.
- [x] `frontend/src/pages/home/HomePage.tsx` + `.module.css` -- the explicit-submit form, the five render branches, `PAGE_SIZE`.
- [x] Tests listed above for every new/changed file.

**Acceptance Criteria:**
- Given the Home page loads with no postings in the catalog, when the initial browse-all search resolves, then "No open postings yet. Check back soon." renders — no card, no Pagination.
- Given postings exist, when the Home page loads, then the browse-all card stack renders immediately (before any submit) with `Pagination` showing "Page 1 of {N}".
- Given a keyword that matches nothing, when the user submits the search form, then `No postings match "{keyword}." Try a different term.` renders (the typed keyword is trimmed and interpolated).
- Given more matching postings than `PAGE_SIZE`, when the user clicks Next, then the query re-runs with `page: 2` and the new page's cards render; Next is disabled once `page === totalPages`, Prev is disabled at `page === 1`.
- Given a search request fails, when the error renders, then clicking the retry button re-issues the identical request (same `submittedQuery`/`page`) rather than resetting to page 1 or an empty keyword.
- Given the CI frontend gates (`lint`, `test:fsd-gate`, `test:api-import-gate`, `lint:tokens`, `test:tokens-gate`, `npm test`, `npm run build`), when they run, then all pass.

## Implementation Notes

## Spec Change Log

## Review Triage Log

Three review layers ran on the diff since `baseline_commit`: blind-hunter (11 findings), edge-case-hunter (4 findings), verification-gap (1 finding). Every raw finding is logged below with its own verdict; grouped routing follows.

| # | Source | Location | Finding | Verdict | Evidence |
|---|---|---|---|---|---|
| 1 | blind-hunter | `pages/home/HomePage.tsx` results `<section>` | No `aria-live` wiring — a screen-reader user gets no announcement when browse-all/no-match/results copy swaps in | low | Verified: only the error banner has `role="alert"`; the other three branches are plain markup. Distinct from the already-deferred 2.2b route-change announcer (that one covers client-side navigation + async settle on a *different* page, not in-place re-search on the same page). |
| 2 | blind-hunter | `entities/job-posting/ui/JobPostingCard.tsx` (`<h3>`) vs `HomePage.tsx` (`<h1>`) | Heading hierarchy skips from `<h1>` straight to each card's `<h3>`, no `<h2>` for the results section | low | Verified: no `<h2>` exists between the hero `<h1>` and the cards. Not a hard WCAG 2.1 AA failure (heading-order is a best-practice technique, not itself an AA success criterion), but a real, cheap gap. |
| 3 | blind-hunter | `HomePage.tsx` — no `placeholderData`/`keepPreviousData` on `useJobPostingSearch` | Changing page drops to the skeleton branch, hiding the just-clicked `Pagination` and previous results during refetch | false | Verified the mechanism is real, but it's not a defect: the frozen Boundaries block states the render precedence as `isPending -> skeleton rows` with no carve-out for pagination refetches, and a brief loading state on page-turn is standard, widely-accepted pagination UX — not a regression the spec asked to avoid. |
| 4 | blind-hunter | `HomePage.tsx` retry button | Retry button never reads `query.isFetching` — no in-flight feedback, nothing stops a repeat click | low | Verified: `onClick={() => query.refetch()}` with no `disabled`/spinner tied to `isFetching`. `refetch()` itself is deduped by TanStack Query (no duplicate network call), so the gap is purely a missing visual affordance, not a functional bug. Same root cause as edge-case-hunter's #14. |
| 5 | blind-hunter | `HomePage.tsx` `handleSubmit` | `setSubmittedQuery(keyword.trim())` never updates `keyword` itself — after submitting `"  engineer  "` the input keeps the untrimmed text while results reflect the trimmed value | low | Verified by reading `handleSubmit`: only `submittedQuery` and `page` are updated, `keyword` (the controlled input's value) is untouched. A real, visible box/results mismatch for leading/trailing-whitespace input. |
| 6 | blind-hunter | `HomePage.tsx` — a whitespace-only keyword trims to `''` | Behaves exactly like browse-all with no dedicated test | false | Verified this is correct behavior by design, not a defect — Intent's "missing/empty query = no filter" applies identically to a whitespace-only submission once trimmed; there is no distinct code path to miss-cover. |
| 7 | blind-hunter | `HomePage.tsx` `const PAGE_SIZE = 20` | Hand-copied literal matching the backend's default, with nothing enforcing the two stay equal | false | Verified there is no live coupling to break: the frontend always sends an explicit `pageSize` on every request (confirmed in `jobPostingsClient.test.ts`'s asserted URLs), so the backend's *implicit* default (used only when `pageSize` is omitted) is never exercised by this app at all — a future backend default change cannot desync anything. |
| 8 | blind-hunter | `backend/Dockerfile` COPY list vs `docker-compose.yml`'s anonymous-volume list | Two module lists must be kept in sync by hand, no cross-reference | low | True, but this is the same pre-existing, unrelated dev-tooling diff already reviewed under 2.3a (added earlier in this session, swept in here only by `baseline_commit` timing) — not caused by this story. |
| 9 | blind-hunter | `docker-compose.yml` `backend`/`frontend` dev services | `backend` has no `healthcheck`; `frontend`'s `depends_on: backend` has no `condition` | low | Same pre-existing dev-tooling gap already logged and deferred under 2.3a's review (identical files/lines). Same root cause as edge-case-hunter's #13. |
| 10 | blind-hunter | `entities/job-posting/api/jobPostingsClient.test.ts` | `search`'s tests cover only a plain keyword and an empty string, not characters needing URL-encoding | false | Rejected per established precedent (2.2b's review): encoding is handled by the generated NSwag client's `encodeURIComponent` call, not by this story's wrapper; testing generated boilerplate was explicitly rejected before for the same reason. |
| 11 | blind-hunter | `HomePage.test.tsx` pending-state test | `document.querySelectorAll('[aria-hidden="true"]')` queries the whole document rather than scoping to the results container | low | Verified: passes today only because nothing else on this isolated page uses `aria-hidden`; a cheap, trivial scoping fix. |
| 12 | edge-case-hunter | `HomePage.tsx` — `page` vs `totalPages` after `total` could shrink between fetches | A shrinking `total` (e.g. postings removed) could leave `page` out of range, rendering an empty area with a mismatched "Page X of Y" | false | Verified unreachable in v1: epics/epic-2-context.md states "v1 has no edit or deactivate lifecycle" — nothing in the product can ever reduce `total` between two fetches, so this precondition cannot occur. |
| 13 | edge-case-hunter | `docker-compose.yml` | `frontend`'s `depends_on: - backend` has no `condition: service_healthy`; `backend` defines no healthcheck | low | Same pre-existing, unrelated dev-tooling gap as #9/#8 above (2.3a's already-deferred finding). |
| 14 | edge-case-hunter | `HomePage.tsx` retry button | Retry clicked while a refetch is already in-flight has no visible feedback | low | Same root cause and evidence as #4 above. |
| 15 | edge-case-hunter | `frontend/vite.config.ts:16` | `process.env.API_PROXY_TARGET ?? 'http://localhost:2052'` doesn't catch an explicit empty-string value | low | Identical code/finding already logged and deferred under 2.3a's review; this line is part of the same pre-existing, unrelated diff, not touched by 2.3b. |
| 16 | verification-gap | `HomePage.tsx` `handleSubmit` / `HomePage.test.tsx` | Page-reset-on-submit (`setPage(1)`) is never tested starting from a page other than the default `1` | medium | Pre-verified by the reviewing layer: the two submit tests never leave `page === 1` before submitting, and the one test that reaches `page: 2` never submits afterward — a regression deleting `setPage(1)` would ship green. Filed disposition: patch. |

**Routing:**
- **patch** — #1 (add `aria-live="polite"` to the results section), #2 (add an `<h2>` for the results region), #4/#14 (disable the retry button while `query.isFetching`), #5 (sync `keyword` to the trimmed value on submit), #11 (scope the pending-state test's `aria-hidden` query to the results container), #16 (add a test: navigate to page 2, then submit a new keyword, assert `search` is called with `page: 1`). Each fix is trivial and self-contained.
- **defer** — #8/#9/#13 (Docker dev-service healthcheck/condition gap) and #15 (`vite.config.ts` empty-string proxy target) are the same pre-existing, unrelated dev-tooling diff already reviewed and deferred under spec-2-3a — not re-appended to `deferred-work.md` to avoid duplicating an entry known, not merely suspected, to be identical.
- **rejected (`false`)** — #3, #6, #7, #10, #12.

## Design Notes

- **Resolves a review-flagged ambiguity from 2.3a.** Code review on 2.3a's diff flagged that this story's original deferred-work.md description ("empty-catalog... when the submitted keyword is empty") didn't also state "and total is 0", leaving it unclear whether an empty keyword against a non-empty catalog should show the empty-catalog message or the results list. Resolved above: the gating condition is `total === 0`, with `submittedQuery` only deciding *which* zero-result copy (empty-catalog vs. no-match) — an empty keyword against a non-empty catalog is the ordinary browse-all success branch.
- **The entity-level `search` wrapper compensates for the generated client's `any` typing.** 2.3a's review deferred fixing the OpenAPI generator's missing `type: integer` on plain `int` properties (systemic — also affects `ProblemDetails.status`) as out of scope and cross-cutting. Rather than let that `any` leak into every call site, the wrapper added here re-types `page`/`pageSize` as `number`, matching the pattern `entities/job-posting/api` already uses to own the boundary between the generated client and the rest of the app.
- **No shared `Skeleton` primitive yet.** 2.2b's Design Notes floated a reusable `shared/ui/Skeleton` as "Story 2.3's call," but the authoritative split (`deferred-work.md`) places `JobPostingCardSkeleton` in `entities/job-posting/ui`, and `PostingDetailPage` already has its own inlined skeleton with no complaint from review. Two data points isn't a pattern yet; extracting a shared primitive from two call sites (one of which is out of this story's scope to touch) is a premature abstraction.

## Verification

**Commands:**
- `cd frontend && npm ci` -- clean install (lockfile unchanged).
- `npm run lint && npm run lint:tokens` -- clean (all colors/spacing/radii via `var(--…)`).
- `npm run test:fsd-gate && npm run test:api-import-gate && npm run test:tokens-gate` -- clean (no boundary violations; the generated client is imported only from `entities/job-posting/api`).
- `npm test -- --run` -- all tests pass, including the new ones listed above.
- `npm run build` -- succeeds.

**Manual checks:**
- `docker compose up`: with an empty catalog, `/` shows the empty-catalog copy. Publish two postings sharing a keyword and a third without it; on `/`, the browse-all view shows all three; submitting the shared keyword narrows to exactly two, each linking to its own detail view; a keyword matching nothing shows the no-match copy with the keyword visible in it.
- Publish enough postings (or set a small `PAGE_SIZE` temporarily) to get a second page; confirm Next/Prev navigate correctly and both disable at their respective bounds.
