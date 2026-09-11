---
title: 'My Applications paged-list endpoint'
type: 'feature'
created: '2026-09-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '8a0766b2af5c45329670773cb2ffdeb243c8737a'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A signed-in Job Seeker who has applied to postings has no way to see the list — `GET /api/applications/mine` (3.1a) only answers "have I applied to this one posting?", never "which postings have I applied to?".

**Approach:** Add `GET /api/applications/mine/list?page=&pageSize=` — a new endpoint and operation id, not an overload of the existing `/mine` route — returning a `Page<MyApplicationListItemResponse>` of the caller's applications, most-recent-first, each row carrying the posting's title resolved via one batched `IJobPostingsApi.GetPostingSummaries` call for the whole page. Mirrors `GET /api/job-postings` (2.3a)'s search-endpoint shape exactly. This is story **3.3a**; the My Applications page + nav item is **3.3b** (`deferred-work.md`).

## Boundaries & Constraints

**Always:**
- New route, new operation id (Decision, 2026-09-12): `GET /api/applications/mine/list`, `.WithName("Applications_GetMyApplications")` — a sibling of the existing `group.MapGet("/mine", ...)` on the same `/api/applications` group, not a second shape on it. Investigated and rejected overloading `/mine` (3.1a's Design Notes had speculatively suggested it): no endpoint anywhere in this codebase returns more than one 200 shape from one route, `OpenApiDocumentTests`'s schema helpers assume exactly one flat schema, and NSwag has no established, verified behavior here for producing an accurate second TypeScript type from a `oneOf` response. A dedicated route sidesteps the risk entirely and lets NSwag emit a clean second client method.
- AD-15 `Page<T>` shape `{ items, page, pageSize, total }`, offset pagination, `page` 1-based, `pageSize` default 20 / max 100 — the exact clamp + overflow-safe `Skip` arithmetic in `SearchJobPostingsHandler` (2.3a), copied verbatim: `page is null or < 1 ? 1 : page.Value`, `pageSize is null or < 1 or > 100 ? 20 : pageSize.Value`, `long` `Skip` math clamped to `int.MaxValue`. `Page<T>` is defined locally in the Applications module (no shared kernel — the same duplication-over-premature-abstraction call 2.3a's `Page<T>` and 3.1a's `Application` entity already made; `NexusJob.Modules.JobPostings`'s `Page<T>` lives in an implementation assembly Applications cannot reference).
- AD-19: posting titles resolved through exactly one `IJobPostingsApi.GetPostingSummaries` batch call for all distinct `job_posting_id`s on the page — never one call per row. `Applications` already references `JobPostings.Contracts` (from 3.1a); no new project reference.
- Rows ordered `SubmittedAt DESC` with an `Id` tiebreaker (mirrors `SearchJobPostingsHandler`'s `OrderByDescending(...).ThenBy(p => p.Id)` for stable pagination). `total` is the raw count of the caller's applications, computed once before any batch resolution or row-dropping (mirrors 2.3a: a page's `total` reflects the DB match count, not the post-resolution row count).
- A row whose posting is absent from the `GetPostingSummaries` batch (a data-integrity violation, unreachable in v1 — nothing deletes a posting) is dropped from `items` rather than failing the whole page, identical to 2.3a's orphan-owner handling and its Design Notes rationale.
- `RequireAuthorization()` + the existing `JobSeekerOnlyEndpointFilter` (401 anonymous, 403 non-Job-Seeker); no antiforgery (a `GET`). Caller identity from the `NameIdentifier` claim, with the same defensive unparseable-claim 401 branch every handler in this module has.
- A new index on `applications.application (job_seeker_id, submitted_at)` (its own EF migration): the existing `UNIQUE (job_posting_id, job_seeker_id)` index has `job_posting_id` as its leading column and cannot serve a `WHERE job_seeker_id = @seeker ORDER BY submitted_at DESC` query efficiently.
- `TreatWarningsAsErrors` stays on; `dotnet build` and the full `dotnet test` (ArchitectureTests, Host.Tests, IntegrationTests) are green, including every unchanged Identity / JobPostings / prior-Applications row.
- `cd frontend && npm run generate:api` is run after a Host build and the regenerated `frontend/src/shared/api/` committed so the CI drift gate stays green; a second run leaves `git status` clean.

**Never:**
- No change to the existing `GET /api/applications/mine?jobPostingId=` endpoint, its request/response shape, or its `Applications_GetMine` operation id — 3.3a adds a sibling route, touches nothing about the 3.1a probe.
- No frontend feature code — the only frontend change is the regenerated `shared/api/`. (3.3b owns the My Applications page, nav item, and skeleton/empty states.)
- No company name, no posting description, no application status/withdraw/ranking in the response — `{ applicationId, jobPostingId, jobPostingTitle, submittedAt }` only; the surface has no scoring, filtering, or status workflow (epic-3-context).
- No shared-kernel `Page<T>` extraction.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Applied to several postings | `GET /api/applications/mine/list?page=1` | `200 Page<...>` with one row per application, most-recent-first, each `jobPostingTitle` correctly resolved | N/A |
| No applications | a freshly-registered Job Seeker, no applications | `200 { items: [], page: 1, pageSize: 20, total: 0 }` | N/A |
| Pagination | more applications than `pageSize` | `page=2` returns the next slice (offset by `pageSize`); `total` reflects the full count on every page | N/A |
| `page`/`pageSize` out of range | `page=0`/negative, or `pageSize=0`/`>100` | clamped to `1` / `20` respectively | N/A |
| Page beyond the last page | `page=999` | `200 { items: [], page: 999, ... }` — not a `404` | N/A |
| Orphan posting (unreachable in v1) | an application row whose `job_posting_id` matches no real posting | that row is dropped from `items`; `total` still reflects the raw application count | N/A |
| Anonymous / Company session | no auth cookie / a `company` `account_type` | `401` / `403` ProblemDetails | N/A |
| OpenAPI document | `GET /openapi/v1.json` | `paths["/api/applications/mine/list"].get` (`Applications_GetMyApplications`) documents optional `page`/`pageSize` and a `200 Page<MyApplicationListItemResponse>` schema; `/mine`'s existing `Applications_GetMine` operation is unchanged | N/A |

</frozen-after-approval>

## Code Map

**Backend — the list slice** (`backend/NexusJob.Modules.Applications/Features/GetMyApplicationsList/`, mirror `JobPostings/Features/SearchJobPostings/`)
- `Page.cs` -- NEW. `public sealed record Page<T>(IReadOnlyList<T> Items, [property: JsonPropertyName("page")] int PageNumber, int PageSize, int Total);` — verbatim copy of `SearchJobPostings/Page.cs`'s shape (module-local; the `PageNumber`/`[JsonPropertyName("page")]` naming avoids the CS0542 self-name collision 2.3a hit).
- `MyApplicationListItemResponse.cs` -- NEW. `public sealed record MyApplicationListItemResponse(string ApplicationId, string JobPostingId, string JobPostingTitle, DateTimeOffset SubmittedAt);`
- `GetMyApplicationsListEndpoint.cs` -- NEW. `internal static class { public static Task<IResult> Handle([FromQuery] int? page, [FromQuery] int? pageSize, [FromServices] GetMyApplicationsListHandler handler, HttpContext httpContext, CancellationToken ct) => handler.HandleAsync(page, pageSize, httpContext, ct); }`.
- `GetMyApplicationsListHandler.cs` -- NEW. `internal sealed class GetMyApplicationsListHandler(ApplicationsDbContext db, IJobPostingsApi jobPostingsApi)`. Resolve `jobSeekerId` from `NameIdentifier` (401 defensive branch). Clamp `page`/`pageSize` (copy `SearchJobPostingsHandler`'s constants + clamp lines verbatim). `var matches = db.Applications.Where(a => a.JobSeekerId == jobSeekerId); var total = await matches.CountAsync(ct);` then page via `.OrderByDescending(a => a.SubmittedAt).ThenBy(a => a.Id).Skip(clampedSkip).Take(clampedPageSize).Select(a => new { a.Id, a.JobPostingId, a.SubmittedAt }).ToListAsync(ct)`. Batch: `var postingIds = rows.Select(r => r.JobPostingId).Distinct().ToArray(); var postings = jobPostingsApi.GetPostingSummaries(postingIds);` then map each row to `MyApplicationListItemResponse`, `continue`-dropping any row whose `JobPostingId` is absent from `postings` (mirror `SearchJobPostingsHandler`'s orphan-drop `continue`). Return `Results.Ok(new Page<MyApplicationListItemResponse>(items, clampedPage, clampedPageSize, total))`.

**Backend — persistence**
- `backend/NexusJob.Modules.Applications/Persistence/ApplicationsDbContext.cs` -- MODIFY `OnModelCreating`: add `entity.HasIndex(e => new { e.JobSeekerId, e.SubmittedAt });` alongside the existing unique pair index.
- `backend/NexusJob.Modules.Applications/Persistence/Migrations/*` -- NEW, generated: `dotnet ef migrations add AddJobSeekerSubmittedAtIndex --project backend/NexusJob.Modules.Applications --startup-project backend/NexusJob.Modules.Applications`. `Up` = `CreateIndex("IX_application_job_seeker_id_submitted_at", schema: "applications", table: "application", columns: new[] { "job_seeker_id", "submitted_at" })`. No table/column change.

**Backend — wiring**
- `backend/NexusJob.Modules.Applications/ApplicationsModule.cs` -- `AddApplicationsModule`: register `services.AddScoped<GetMyApplicationsListHandler>();`. `MapApplicationsModule`: on the existing `group`, add `group.MapGet("/mine/list", GetMyApplicationsListEndpoint.Handle).WithName("Applications_GetMyApplications").RequireAuthorization().AddEndpointFilter<JobSeekerOnlyEndpointFilter>().Produces<Page<MyApplicationListItemResponse>>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status403Forbidden);` — placed so `/mine/list` is not shadowed by the existing `/mine` route (distinct path segments; order does not matter here, but keep it directly below the `/mine` mapping for readability).

**Backend — tests** (`backend/NexusJob.IntegrationTests/`)
- `TestSupport.cs` -- add to `ApplicationsDatabase`: `InsertApplicationAsync(Guid jobPostingId, Guid jobSeekerId, DateTimeOffset submittedAt)` (raw `INSERT`, Guid v7 id generated in the helper — mirrors `JobPostingsDatabase.InsertPostingAsync`'s orphan-row seeding pattern) for controlling row order/count and seeding an orphan posting id in tests. Add `AuthApiClient.GetMyApplicationsAsync(int? page, int? pageSize)` (anonymous-shaped GET, no CSRF, builds the query string — mirror `SearchPostingsAsync`). Add `PageDto<T>` reuse (already exists from 2.3a) and `MyApplicationListItemDto(applicationId, jobPostingId, jobPostingTitle, submittedAt)`.
- `GetMyApplicationsListEndpointTests.cs` -- NEW. `[Collection(nameof(IdentityApiCollection))]`. One `[Fact]`/`[Theory]` per I/O-matrix row: several applications ordered most-recent-first with correct titles (two distinct postings, proving per-row resolution is correct — the same indirect proof of batching 2.3a's multi-company test used, since an integration test cannot intercept the in-process `IJobPostingsApi` call count); no applications → empty page; pagination (seed > `pageSize` rows via `InsertApplicationAsync`, assert page 2's slice + `total`); `page`/`pageSize` clamping; page beyond last; orphan posting via `InsertApplicationAsync` with a random non-existent posting id → dropped from `items`, `total` unchanged; anonymous → 401; Company session → 403.
- `OpenApiDocumentTests.cs` -- NEW test mirroring `Document_describes_the_job_postings_search_operation_with_its_query_params_and_200_shape` exactly: assert `paths["/api/applications/mine/list"].get`, `operationId` `Applications_GetMyApplications`, `page`/`pageSize` optional query params, `200` schema `{ items[], page, pageSize, total }` with item shape `{ applicationId, jobPostingId, jobPostingTitle, submittedAt }`. Also assert the existing `/mine` path's `Applications_GetMine` operation is untouched (same assertions as today).

**Frontend**
- `frontend/src/shared/api/*` -- run `npm run generate:api` after a Host build; commit the regenerated client. NSwag adds a new `ApplicationsClient.getMyApplications(page?, pageSize?)` method and `MyApplicationListItemResponse` / `PageOfMyApplicationListItemResponse` types, leaving `getMine` untouched. No hand edits; a second run is drift-clean.

**Reference — do not change**
- `backend/NexusJob.Modules.JobPostings/Features/SearchJobPostings/{Page.cs,SearchJobPostingsHandler.cs,SearchJobPostingsEndpoint.cs}` -- the paged-list slice shape, clamp/overflow-safe-skip arithmetic, and batched-resolution-with-orphan-drop pattern being mirrored.
- `backend/NexusJob.Modules.Applications/Features/GetMyApplication/**` -- the existing single-posting probe (`Applications_GetMine`); untouched, do not modify or rename.
- `backend/NexusJob.Modules.JobPostings.Contracts/IJobPostingsApi.cs` -- `GetPostingSummaries` is the batch getter this handler must use.
- `_bmad-output/implementation-artifacts/spec-3-1a-applications-module-and-apply-endpoints.md` -- continuity: the `Application` entity, `ApplicationsDbContext`, `JobSeekerOnlyEndpointFilter`, and the module's existing test fixture/helpers this story extends.

## Tasks & Acceptance

**Execution:**
- [x] `backend/NexusJob.Modules.Applications/Features/GetMyApplicationsList/{Page,MyApplicationListItemResponse,GetMyApplicationsListEndpoint,GetMyApplicationsListHandler}.cs` -- the paged-list slice (clamp, batch-resolve, orphan-drop).
- [x] `backend/NexusJob.Modules.Applications/Persistence/ApplicationsDbContext.cs` + `Persistence/Migrations/*` -- the `(job_seeker_id, submitted_at)` index and its migration.
- [x] `backend/NexusJob.Modules.Applications/ApplicationsModule.cs` -- register the handler; map `GET /mine/list` (`Applications_GetMyApplications`, Job-Seeker-only, `.Produces<Page<...>>(200)`).
- [x] `backend/NexusJob.IntegrationTests/TestSupport.cs` -- `ApplicationsDatabase.InsertApplicationAsync`; `AuthApiClient.GetMyApplicationsAsync`; `MyApplicationListItemDto`.
- [x] `backend/NexusJob.IntegrationTests/GetMyApplicationsListEndpointTests.cs` -- one test per I/O-matrix row.
- [x] `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` -- assert the new operation's schema and that `Applications_GetMine` is unchanged.
- [x] `frontend/src/shared/api/*` -- `npm run generate:api` after a Host build; commit; verify drift-clean on re-run.

**Acceptance Criteria:**
- Given a Job Seeker who has applied to postings from two different companies, when `GET /api/applications/mine/list?page=1` is called, then the response is `200 Page<MyApplicationListItemResponse>` with both rows' `jobPostingTitle` correct and rows ordered most-recent-first.
- Given more applications than `pageSize`, when `page=2` is requested, then `items` is the next page's slice and `total` reflects the full count regardless of page.
- Given `dotnet build backend/NexusJob.sln` and `dotnet test backend/NexusJob.sln`, when they run, then the build is warning-free and every suite passes — the boundary gates, the unchanged 3.1a `Applications_GetMine` suite, and the new list + OpenAPI tests.
- Given a Host build then `cd frontend && npm run generate:api`, when it runs, then `frontend/src/shared/api/` gains `ApplicationsClient.getMyApplications`, `getMine` is unchanged, a second run leaves `git status` clean, and `npm run lint`/`npm test`/`npm run build` pass.

## Implementation Notes

## Spec Change Log

## Review Triage Log

Three layers ran on the diff since `baseline_commit`: blind-hunter (10 findings), edge-case-hunter (1 finding), verification-gap (0 findings — no gaps between the spec's claims and the shipped code/tests).

| # | Source | Location | Finding | Verdict | Evidence |
|---|---|---|---|---|---|
| 1 | edge-case-hunter | `GetMyApplicationsListHandler.cs` `CountAsync` + paged `Skip`/`Take` | Two separate DB round-trips; a write to the caller's own applications between them could make `total` disagree with `items`, or skip/duplicate a row across pages. | false | Identical to a finding already triaged `false` on 2.3a's `SearchJobPostingsHandler` (the exact pattern this handler mirrors verbatim): nothing in the Intent/ACs promises snapshot-consistent pagination under concurrent writes, and this is the established non-transactional offset-pagination pattern used everywhere else in the codebase. |
| 2 | blind-hunter | `frontend/src/shared/api/nexus-api-client.ts` | `getMyApplications`'s `page`/`pageSize` params and `PageOfMyApplicationListItemResponse.{page,pageSize,total}` are typed `any`. | low | Verified: same systemic cause already tracked for 2.3a's `search` client (the OpenAPI generator omits `type: integer` for plain `int` properties) — a fresh instance of a known, deferred, cross-cutting gap, not a new defect. Appended to `deferred-work.md`. |
| 3 | blind-hunter | `epic-3-context.md` | Still lists a single un-split "Story 3.3", with no mention of the 3.3a/3.3b split. | low | Same documentation-drift class already logged for the 3.1a/3.1b split; the 3.3a spec itself is precise, so no implementer was misled. Appended to `deferred-work.md`. |
| 4 | blind-hunter | `sprint-status.yaml` | The `3-3-...` line has no inline comment explaining the 3.3a/3.3b split, unlike the `3-1-...` line. | low | Addressed procedurally: step-05 of this workflow adds the explanatory comment when the story reaches `review`, mirroring 3.1's and 3.2's lines. No action needed here. |
| 5 | blind-hunter | Spec frontmatter vs. `sprint-status.yaml` | Spec `status: 'in-review'` while the sprint tracker still reads `in-progress`. | false | Expected sequencing, not a discrepancy: this review step runs before step-05 syncs the sprint key to `review`. |
| 6 | blind-hunter | EF migration timestamp vs. spec `created` date | The migration file is timestamped Sep 11 21:38, before the spec's `created: '2026-09-12'`. | false | A harmless session-clock artifact (the migration was generated shortly before local midnight); migration timestamps are ordering keys only, unrelated to a spec's `created` field, with no functional or data consequence. |
| 7 | blind-hunter | `GetMyApplicationsListEndpointTests.cs` clamp tests | Only `page=0` and `pageSize` `0`/`500` are tested; no negative values, and `pageSize=100` (the max, should pass through unclamped) is untested. | low / patched | The negative-value half is `false` by the same reasoning already accepted for 2.3a: `page is null or < 1 ? 1 : ...` is one boolean branch with no distinct code path between `0` and a negative value. The `pageSize=100` boundary is a real, cheap-to-close gap — added `The_maximum_page_size_of_100_passes_through_unclamped`, pinning the clamp's inclusive upper bound. |
| 8 | blind-hunter | Orphan-page vs. page-beyond-last | Both can return `items: []` with a non-zero `total`, and nothing distinguishes them over the wire. | false | Both existing tests already assert the correct `total` for their own scenario; a client already has everything it needs (`items.length` vs `total`) to reason about either case — this is normal paginated-API behavior, not an ambiguity the API needs to resolve further. |
| 9 | blind-hunter | `OpenApiDocumentTests.cs` new test | `itemsSchema`/`.GetProperty("items")` naming is confusing. | false | Copied verbatim from the existing, already-shipped 2.3a `JobPostings_Search` OpenAPI test this story was told to mirror "exactly" — pre-existing style, not introduced by this diff. |
| 10 | blind-hunter | `OpenApiDocumentTests.cs` new test | No assertion that `Applications_GetMyApplications` carries the same `tags` grouping as `Applications_GetMine`. | false | No test anywhere in this codebase's `OpenApiDocumentTests.cs` asserts operation `tags`; not an established convention this diff should be first to introduce. |
| 11 | blind-hunter | New 401/403 tests | Assert only `Status` on the ProblemDetails body, never `Title`/`Detail`. | false | Matches the established depth of every other 401/403 test in this codebase (the same finding was raised and rejected on 3.1b on identical grounds). |

**Routing** (survivors grouped by shared root cause; `false` findings rejected outright):
- **patch** — #7's `pageSize=100` boundary test (applied and verified: 11/11 pass in the new test file).
- **defer** — #2 (NSwag `any`-typing, systemic pre-existing gap), #3 (`epic-3-context.md` split staleness, same class as the 3.1a/3.1b entry). Both appended to `deferred-work.md`.
- **no action** — #4 (procedural, handled at step-05), #1/#5/#6/#8/#9/#10/#11 (false, logged with refutation above).

## Design Notes

- **Response fields.** `{ applicationId, jobPostingId, jobPostingTitle, submittedAt }` — the epic's AC names only "the posting's title," but a list row that cannot link to the posting or show when it was submitted would be unusable; `applicationId`/`jobPostingId` cost nothing extra (already on the row) and `submittedAt` is the sort key made visible. No company name, description, or status — out of this epic's scope (epic-3-context: "no scoring, ranking, filtering, or status workflow").
- **Why a new route instead of overloading `/mine`.** See the frozen Decision. The concrete risk: NSwag names generated client methods from `operationId`; keeping one `Applications_GetMine` id for two shapes means NSwag either collapses the OpenAPI 200 response to one schema (silently wrong for whichever shape loses) or — if a `oneOf` is somehow produced — very likely emits `any` for the client method's return type, the same failure mode already on record for `JobPostingsClient.search`'s `page`/`pageSize` params (deferred-work.md, 2.3a). A second, cleanly named operation avoids gambling on untested tooling behavior for a story that doesn't need to share the route.
- **Integration tests cannot literally assert "exactly one batched call."** Like 2.3a's `IIdentityApi.GetCompanies` test, this suite proves batching indirectly: a multi-posting page where every row's title resolves correctly is the observable consequence of correct batch-resolution code, since these are real in-process contract implementations, not mocks a test could intercept.

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings, 0 errors.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass, including `GetMyApplicationsListEndpointTests`, the extended `OpenApiDocumentTests`, and every unchanged Identity/JobPostings/Applications/Architecture/Host suite (Docker required for Testcontainers `postgres:18`).
- `dotnet run --project backend/NexusJob.Host` then, as a Job Seeker who has applied to a posting, `curl -sb "cookie-jar" localhost:2052/api/applications/mine/list | jq` -- expected: `200 { items: [{...}], page: 1, pageSize: 20, total: 1 }`.
- `cd frontend && npm ci && npm run generate:api && git status --porcelain src/shared/api` -- expected: gains `getMyApplications` on the first run; empty on a re-run.
- `npm run lint && npm test -- --run && npm run build` -- expected: all green with the regenerated client.

**Manual checks:**
- `docker compose up`, apply to two postings as one Job Seeker (from two different Companies) → `GET /api/applications/mine/list` shows both, most-recent first, correct titles. A second Job Seeker with no applications → empty page.
- `psql`: `applications.application` gains the `IX_application_job_seeker_id_submitted_at` index; `identity` and `job_postings` schemas untouched.
