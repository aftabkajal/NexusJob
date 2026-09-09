---
title: 'Search job postings endpoint'
type: 'feature'
created: '2026-09-09'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e9fce592077daeb8b6883167e30382fc9521e6f2'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Companies can create and read a single posting, but nothing can find one — there is no search endpoint, so the Home page's search bar and browse listing have nothing real to call.

**Approach:** Add an anonymous `GET /api/job-postings?query=&page=&pageSize=` slice returning `Page<JobPostingSearchResultResponse>` — a case-insensitive `ILIKE` substring match over title/description, company names resolved via one batched `IIdentityApi.GetCompanies` call for the whole page. This is story **2.3a**; the Home page search/browse surface is **2.3b** (`deferred-work.md`).

## Boundaries & Constraints

**Always:**
- AD-15: `Page<T> { items, page, pageSize, total }`, offset pagination, `page` 1-based, `pageSize` default 20; clamp `page < 1` to `1` and `pageSize` outside `[1,100]` to `20`. AD-18: the endpoint is `AllowAnonymous`; the match is `EF.Functions.ILike` (Npgsql) over title OR description — never a search engine or index service. AD-5/AD-19: company names come only from a single `identityApi.GetCompanies(ownerIds)` batch call for all rows on the page — never one `GetCompany` per row. AD-14: delegate → DI-resolved slice handler, no mediator.
- `query` is optional; missing/empty means no filter (an empty pattern matches every row) — this is how a plain browse listing and a keyword search are the same request. Escape literal `%`, `_`, `\` in the keyword before building the `ILIKE` pattern so those characters match literally, not as SQL wildcards.
- Rows order newest-first (`ORDER BY created_at DESC`) — no other sort is offered.
- Response item shape is `{ id, title, description, companyName }` (string id; no `createdAt` — matches 2.2b's precedent of no posted-date on the surface).
- If a row's owner Company is unexpectedly absent from the `GetCompanies` batch (data-integrity violation, unreachable in v1 — no company deletion), drop that row from the page rather than fail the whole request (see Design Notes).
- `TreatWarningsAsErrors` stays on; `dotnet build` and the full `dotnet test` (ArchitectureTests, Host.Tests, IntegrationTests) are green, including every unchanged 2.1/2.2/Identity/Architecture row.
- `cd frontend && npm run generate:api` is run and the regenerated `frontend/src/shared/api/` committed so the CI drift gate stays green; a second run leaves `git status` clean.

**Never:**
- No frontend feature code — no search bar wiring, `job-card`, pagination control, or route change. The only frontend change is the regenerated `shared/api/`. (2.3b owns the surface.)
- No EF migration, schema change, or new index — read-only over the existing `job_posting` table (a full-text search engine / read model is explicitly deferred).
- No location/salary/other filter facet, no cursor pagination.
- No shared-kernel extraction — `Page<T>` stays local to this module (see Design Notes).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Browse, no keyword | `GET /api/job-postings?page=1` (no `query`) | `200 Page<...>` with every posting, newest first | N/A |
| Keyword matches some | `GET ?query=engineer` | `200` with only matching rows; each `companyName` resolved via the batched call | N/A |
| Keyword matches none | `GET ?query=zzzz` (postings exist) | `200 { items: [], total: 0 }` | N/A |
| No postings exist at all | `GET ?query=` | `200 { items: [], total: 0 }` | N/A |
| Literal `%`/`_` in keyword | `GET ?query=50%25` (a literal `%`) | matches only postings containing the literal substring `50%` — the character is escaped, not a wildcard | N/A |
| Page beyond the last page | `GET ?page=999` | `200 { items: [], page: 999, pageSize: 20, total: N }` — not a `404` | N/A |
| `pageSize` out of range | `GET ?pageSize=0` or `?pageSize=500` | clamped to `20` | N/A |
| Owner Company unresolved (unreachable in v1) | a row's `ownerCompanyId` missing from the `GetCompanies` batch | that row is dropped from `items`; `total` still reflects the DB match count | N/A |
| Multi-company page | two postings from two different companies both match | both rows carry the correct `companyName`, resolved by exactly one `GetCompanies` call | N/A |
| OpenAPI document | `GET /openapi/v1.json` | `paths["/api/job-postings"].get` (`JobPostings_Search`) documents optional `query`/`page`/`pageSize` and a `200 Page<JobPostingSearchResultResponse>` schema | N/A |

</frozen-after-approval>

## Code Map

**Backend — the search slice** (`backend/NexusJob.Modules.JobPostings/Features/SearchJobPostings/`)
- `Page.cs` -- NEW. `public sealed record Page<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);` (AD-15 shape; module-local, not a `.Contracts` type — no shared kernel exists yet, see Design Notes).
- `JobPostingSearchResultResponse.cs` -- NEW. `public sealed record JobPostingSearchResultResponse(string Id, string Title, string Description, string CompanyName);`
- `SearchJobPostingsEndpoint.cs` -- NEW. `internal static class SearchJobPostingsEndpoint { public static Task<IResult> Handle([FromQuery] string? query, [FromQuery] int? page, [FromQuery] int? pageSize, [FromServices] SearchJobPostingsHandler handler, CancellationToken ct) => handler.HandleAsync(query, page, pageSize, ct); }`
- `SearchJobPostingsHandler.cs` -- NEW. `internal sealed class SearchJobPostingsHandler(JobPostingsDbContext db, IIdentityApi identityApi)`. Clamps `page`/`pageSize`; escapes `%`/`_`/`\` in `query ?? ""`; builds `db.JobPostings.Where(p => EF.Functions.ILike(p.Title, pattern) || EF.Functions.ILike(p.Description, pattern))`; `total = await CountAsync`; page rows via `OrderByDescending(p => p.CreatedAt).Skip(...).Take(...)`; batches distinct `OwnerCompanyId`s through `identityApi.GetCompanies`; maps to `JobPostingSearchResultResponse`, dropping any row whose owner is absent from the batch; returns `Results.Ok(new Page<JobPostingSearchResultResponse>(items, page, pageSize, total))`.
- `backend/NexusJob.Modules.JobPostings/JobPostingsModule.cs` -- `AddJobPostingsModule`: register `SearchJobPostingsHandler`. `MapJobPostingsModule`: on the existing `group`, add `group.MapGet("", SearchJobPostingsEndpoint.Handle).WithName("JobPostings_Search").AllowAnonymous().Produces<Page<JobPostingSearchResultResponse>>(StatusCodes.Status200OK);`

**Backend — tests** (`backend/NexusJob.IntegrationTests/`)
- `TestSupport.cs` -- add `AuthApiClient.SearchPostingsAsync(string? query, int? page, int? pageSize)` (anonymous `GET`, builds the query string); add `PageDto<T>` / `JobPostingSearchResultDto` records for deserialization.
- `JobPostingSearchEndpointTests.cs` -- NEW. `[Collection(nameof(IdentityApiCollection))]`. One `[Fact]` per I/O-matrix row: browse-all (no `query`), keyword match/no-match, empty catalog, literal `%`/`_` escaping, page-beyond-last, `pageSize` clamp, and the multi-company page (both rows' `companyName` correct — the indirect proof that batching, not per-row lookup, is used).
- `OpenApiDocumentTests.cs` -- extend: assert `paths["/api/job-postings"].get`, `operationId` `JobPostings_Search`, `query`/`page`/`pageSize` as optional query parameters, `200` schema `{ items[], page, pageSize, total }`.

**Frontend**
- `frontend/src/shared/api/*` -- run `npm run generate:api` after a Host build; commit the regenerated client. NSwag adds a `JobPostingsClient.search(query?, page?, pageSize?)` method returning a new `Page` / `JobPostingSearchResultResponse` type. No hand edits; a second run is drift-clean.

**Reference — do not change**
- `backend/NexusJob.Modules.JobPostings/Features/GetJobPostingById/**` -- the slice shape being mirrored.
- `backend/NexusJob.Modules.Identity.Contracts/IIdentityApi.cs` -- `GetCompanies` is the batch getter this handler must use (never `GetCompany` per row).
- `_bmad-output/implementation-artifacts/spec-2-2a-detail-endpoint-and-contracts.md` -- continuity: the `IIdentityApi.GetCompanies` batch shape, the `MapGroup`/slice/`WithName` conventions, the `IdentityApiCollection` fixture + `AuthApiClient` helpers, the `OpenApiDocumentTests` assertion pattern.

## Tasks & Acceptance

**Execution:**
- [x] `backend/NexusJob.Modules.JobPostings/Features/SearchJobPostings/{Page,JobPostingSearchResultResponse,SearchJobPostingsEndpoint,SearchJobPostingsHandler}.cs` -- the anonymous search slice (clamp, escape, batched company resolution).
- [x] `backend/NexusJob.Modules.JobPostings/JobPostingsModule.cs` -- register the handler; map `GET ""` (`JobPostings_Search`, `.AllowAnonymous()`, `.Produces<Page<...>>(200)`).
- [x] `backend/NexusJob.IntegrationTests/TestSupport.cs` -- `AuthApiClient.SearchPostingsAsync(...)`; the `PageDto<T>` / result DTO.
- [x] `backend/NexusJob.IntegrationTests/JobPostingSearchEndpointTests.cs` -- one test per I/O-matrix row.
- [x] `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` -- assert `GET /api/job-postings` `JobPostings_Search` with its query params + `200` schema.
- [x] `frontend/src/shared/api/*` -- `npm run generate:api` after a Host build; commit; verify drift-clean on re-run.

**Acceptance Criteria:**
- Given postings from two different companies, when `GET /api/job-postings?query={keyword}` matches both, then the response is `200 Page<JobPostingSearchResultResponse>` with both rows' `companyName` correct and exactly one `IIdentityApi.GetCompanies` call made for the page.
- Given more matching postings than `pageSize`, when `page=2` is requested, then the response's `items` are the next page's rows (offset by `pageSize`), `page` echoes `2`, and `total` reflects the full match count regardless of page.
- Given `dotnet build backend/NexusJob.sln` and `dotnet test backend/NexusJob.sln`, when they run, then the build is warning-free and every suite passes — the boundary/raw-SQL gates, the unchanged 2.1/2.2 suites, and the new search + OpenAPI tests.
- Given a Host build then `cd frontend && npm run generate:api`, when it runs, then `frontend/src/shared/api/` gains the search client method, a second run leaves `git status` clean, and `npm run lint`/`npm test`/`npm run build` pass.

## Implementation Notes

- **`Page<T>`'s positional parameter is `PageNumber`, not `Page`.** The Code Map's literal
  `public sealed record Page<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);`
  does not compile: C# (CS0542) forbids a member sharing its enclosing type's simple name, and
  `Page<T>`'s own name is `Page`. The type name and file (`Page.cs`) are unchanged; the
  conflicting parameter is renamed to `PageNumber` with `[property: JsonPropertyName("page")]`
  so the wire shape stays exactly `{ items, page, pageSize, total }` per AD-15. Verified via the
  OpenAPI document (`components.schemas.PageOfJobPostingSearchResultResponse.properties.page`)
  and every integration test's `PageDto<T>` deserialization.
- **Npgsql's two-argument `EF.Functions.ILike(match, pattern)` disables escaping outright.**
  It translates to `... ILIKE pattern ESCAPE ''` - an explicit *empty* escape string - not
  Postgres's own default (backslash) that a raw `ILIKE` with no `ESCAPE` clause would use. With
  the two-arg overload, the handler's escaped pattern (`50\%` for a `50%` keyword) was matched
  literally against a required literal backslash and never matched anything, silently breaking
  the AC's "substring match" guarantee for every keyword containing `%`/`_`. Confirmed by
  reproducing against a real Postgres container with EF Core command logging. Fixed by using the
  three-argument overload, `EF.Functions.ILike(p.Title, pattern, "\\")`, which pins the escape
  character back to backslash to match `EscapeLikePattern`'s output. Covered by
  `Literal_percent_in_the_keyword_matches_only_the_literal_substring`, which failed under the
  two-arg overload and passes under the three-arg one.
- **The matrix's "no postings exist at all" row can't be constructed against the shared test
  fixture.** `IdentityApiFixture`'s Postgres container (and therefore `job_postings.job_posting`)
  is shared across every test class in `IdentityApiCollection`, so rows from earlier-run test
  classes are already present by the time `JobPostingSearchEndpointTests` runs - there is no way
  to observe a truly empty table. Implemented as the semantically equivalent, testable claim: an
  explicit `query=` behaves identically to an omitted `query` (Intent's "missing/empty means no
  filter"), via `Search_with_an_explicit_empty_query_string_is_treated_as_no_filter`. Every other
  row is scoped to postings the test itself creates, keyed by a fresh GUID-derived marker
  embedded in the title/description, so assertions never depend on the table's total contents.

## Spec Change Log

- 2026-09-09 -- Implementation deviation (non-frozen section, Code Map): `Page<T>`'s positional
  parameter `Page` renamed to `PageNumber` (`[JsonPropertyName("page")]` keeps the wire shape
  unchanged) because the literal declaration does not compile (CS0542: member name collides with
  the enclosing generic type's simple name `Page`). See Implementation Notes.

## Review Triage Log

Three review layers ran on the diff since `baseline_commit`: blind-hunter (11 findings), edge-case-hunter (5 findings), verification-gap (1 finding). Every raw finding is logged below with its own verdict; grouped routing (patch/defer) follows.

| # | Source | Location | Finding | Verdict | Evidence |
|---|---|---|---|---|---|
| 1 | blind-hunter | `frontend/src/shared/api/nexus-api-client.ts` | `search()`'s `page`/`pageSize` params and `PageOfJobPostingSearchResultResponse.{page,pageSize,total}` are typed `any` | medium | Verified: the generated client's signatures read `page: any \| undefined`, `pageSize: any \| undefined`; the OpenAPI schema for these three properties carries only `pattern`/`format:int32`, no `type`, so NSwag falls back to `any`. |
| 2 | blind-hunter | `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` | New OpenAPI test never asserts parameter/property *types* | low | Verified: `Document_describes_the_job_postings_search_operation_...` only checks name/`required:false`; would not have caught #1. Same root cause as #1/#15. |
| 3 | blind-hunter | `backend/NexusJob.IntegrationTests/JobPostingSearchEndpointTests.cs` | No test proves a match existing only in `description` | low | Verified: every test's marker is embedded in the posting `title`; none isolates a description-only match, so the handler's `OR EF.Functions.ILike(p.Description, ...)` branch is untested. |
| 4 | blind-hunter | `backend/NexusJob.IntegrationTests/JobPostingSearchEndpointTests.cs` | No test verifies case-insensitivity | low | Verified: no test uses a query differently-cased from the stored text, despite the handler's doc comment and spec Design Notes calling out "case-insensitive ILIKE" as delivered behavior. |
| 5 | blind-hunter | `docker-compose.yml`, `backend/Dockerfile`, `frontend/Dockerfile` | Docker dev-tooling additions aren't mentioned in this spec | low | True, but these files predate 2.3a's implementation (added earlier in this session, unrelated to the search endpoint) — not scope creep by this story. Same root cause as #8/#9/#11/#16/#17. |
| 6 | blind-hunter | `SearchJobPostingsHandler.cs:43` | Only `page=0` tested for the `<1` clamp, not a negative value | false | Verified: `page is null or < 1 ? 1 : page.Value` is one boolean branch covering every value below 1 identically — no distinct code path exists for negative vs. zero that the tested `page=0` case doesn't already exercise. |
| 7 | blind-hunter | `OpenApiDocumentTests.cs:241-261` | New `ResolveSchema` helper duplicates `ResolveSchemaProperties`'s `$ref`-resolution logic | low | Verified by reading both methods: identical 4-line `$ref` → `components.schemas[name]` unwrap block appears in each. |
| 8 | blind-hunter | `docker-compose.yml` | `backend` dev service has no `healthcheck`; `frontend`'s `depends_on: backend` has no `condition` | low | True; part of the pre-existing, unrelated dev-tooling diff (#5 group). |
| 9 | blind-hunter | `.claude/scheduled_tasks.lock` | Session-lock file churn included in the diff | low | True; a runtime artifact unrelated to any code path, pre-existing. Part of #5 group. |
| 10 | blind-hunter | `_bmad-output/implementation-artifacts/deferred-work.md` | 2.3b entry's empty-catalog condition omits the "total is 0" qualifier | low | Verified: wording is ambiguous, but this describes a not-yet-started future story's requirements (written when 2.3 was split), not 2.3a's shipped behavior. |
| 11 | blind-hunter | `docker-compose.yml` | Local Postgres password hardcoded a third time | low | True; no new secret exposure (same known local-dev placeholder already duplicated twice before this diff). Part of #5 group. |
| 12 | edge-case-hunter | `SearchJobPostingsHandler.cs:43,57` | `(clampedPage-1)*clampedPageSize` can overflow `Int32` for a very large `page`, wrapping negative | medium | Verified: `Directory.Build.props` sets no `CheckForOverflowUnderflow` (default unchecked); a `page` ≥ ~21,474,838 overflows and wraps negative; Postgres rejects a negative `OFFSET` with an unhandled exception instead of the matrix's established "page beyond last → empty page" behavior. |
| 13 | edge-case-hunter | `SearchJobPostingsHandler.cs:56` | No deterministic tie-breaker on `OrderByDescending(CreatedAt)` | low | Verified: no secondary sort key; two rows with an identical timestamp at a page boundary have no guaranteed stable order. Low likelihood given microsecond precision and normal single-request writes. |
| 14 | edge-case-hunter | `SearchJobPostingsHandler.cs:53-60` | `CountAsync` and the paged `ToListAsync` are separate round-trips (read skew under concurrent writes) | false | Verified the scenario can occur but violates no documented guarantee — nothing in Intent/ACs promises snapshot-consistent pagination under concurrent writes; same non-transactional offset-pagination pattern used elsewhere in the codebase. |
| 15 | edge-case-hunter | `nexus-api-client.ts` | Same `any`-typing root cause as #1 | medium | Same evidence as #1. |
| 16 | edge-case-hunter | `frontend/vite.config.ts:16` | `process.env.API_PROXY_TARGET ?? 'http://localhost:2052'` doesn't catch an empty-string value | low | True as a code fact (`??` only falls back on null/undefined), but no shipped config sets it to `""` today. Part of #5 group. |
| 17 | verification-gap | `docker-compose.yml`, `backend/Dockerfile`, `frontend/Dockerfile`, `.github/workflows/ci.yml` | `dev`-profile Docker workflow has zero CI coverage | medium | Pre-verified by the reviewing layer (grepped `.github` for `profile`/`Dockerfile`; confirmed `ci.yml`'s only Docker job never passes `--profile dev`). Filed disposition: defer. Part of #5 group. |

**Routing** (survivors grouped by shared root cause; `false` findings above are rejected outright):
- **patch** — #12 (page-overflow clamp), #13 (CreatedAt tie-breaker), #3 (description-only-match test), #4 (case-insensitivity test), #7 (ResolveSchema duplication). Each fix is trivial, self-contained, adds no public surface. Sent to the implementation subagent for the smallest fix.
- **defer** — #1/#2/#15 (OpenAPI generator omits `type: integer` for plain `int` properties — confirmed systemic via the pre-existing `ProblemDetails.status`, not introduced by this story); #5/#8/#9/#11/#16/#17 (pre-existing, unrelated Docker/Vite dev-tooling swept into this diff by `baseline_commit` timing, including its own minor gaps and its lack of CI coverage); #10 (deferred-work.md 2.3b wording, predates this story). Appended to `deferred-work.md`.

## Design Notes

- **`Page<T>` stays module-local, not a shared kernel.** No shared/common backend project exists yet, and prior stories' "Never" lists explicitly rule out a premature shared-kernel extraction. `Page<T>` is defined once inside `JobPostings.Features.SearchJobPostings`; a later module (Epic 3's Applications) that needs the identical shape duplicates the same four-property record rather than forcing a new shared project now.
- **Escaping is a correctness fix, not a policy choice.** `EF.Functions.ILike` compiles to Postgres `ILIKE`, where a literal `%`/`_`/`\` in user input is otherwise treated as a wildcard; escaping them before building the pattern is required for the AC's "substring match" to hold for any keyword.
- **Dropping an orphan-owner row instead of failing the page.** 2.2a's detail endpoint returns a `500` for the same unreachable data-integrity case because it reads one row; a search page reads many, so one bad row failing the whole page would be a worse regression for a case that cannot occur in v1 (no company deletion).

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings, 0 errors.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass, including `JobPostingSearchEndpointTests`, the extended `OpenApiDocumentTests`, and every unchanged 2.1/2.2/Identity/Architecture/Host suite (Docker required for Testcontainers `postgres:18`).
- `dotnet run --project backend/NexusJob.Host` then `curl -s "localhost:2052/api/job-postings?page=1" | jq` -- expected: `200 { items, page: 1, pageSize: 20, total }`.
- `cd frontend && npm ci && npm run generate:api && git status --porcelain src/shared/api` -- expected: the client gains `search` on the first run; empty on a re-run.
- `npm run lint && npm test -- --run && npm run build` -- expected: all green with the regenerated client.

**Manual checks:**
- `docker compose up`, register two Companies, each publishes a posting sharing a keyword in the title/description, a third posting from either without the keyword → `GET /api/job-postings?query={keyword}` returns exactly the two matching rows with correct `companyName`s; the third is excluded.
- `identity` and `job_postings` schemas are untouched — no migration ran; `__EFMigrationsHistory` in both is unchanged.
