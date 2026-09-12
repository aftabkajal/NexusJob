---
title: 'Applicants and My Postings backend'
type: 'feature'
created: '2026-09-12'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '9def28b22f8372e3a2fef39ad13d4489ebe49a43'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A Company that has posted jobs has no way to see its own postings or who applied to any of them — closing the epic's core loop end to end requires both a "my postings" list and an owner-scoped applicant list, and neither exists yet.

**Approach:** Add three backend pieces across three modules: `GET /api/job-postings/mine?page=&pageSize=` (JobPostings) listing the caller's own postings; `IIdentityApi.GetJobSeeker`/`GetJobSeekers` (Identity.Contracts) for resolving an applicant's name and email; and `GET /api/applications?jobPostingId=&page=&pageSize=` (Applications) — an owner-scoped applicant list whose ownership check yields a byte-identical `404` for both "no such posting" and "not my posting," never a `403`. This is story **3.4a**; the My Postings + Applicants frontend surface is **3.4b** (`deferred-work.md`) — the last split in Epic 3.

## Boundaries & Constraints

**Always:**
- `GET /api/job-postings/mine` — Company-only (`RequireAuthorization()` + the existing `JobPostings.Auth.CompanyOnlyEndpointFilter`, no antiforgery on a `GET`). Scoped by `WHERE owner_company_id = {caller's NameIdentifier}`, ordered `CreatedAt DESC` with an `Id` tiebreaker. Reuses the module's own `SearchJobPostings.Page<T>` (same module, sibling feature folder — no cross-module boundary crossed, so no new duplicate). Clamp constants and overflow-safe `Skip` copied verbatim from `SearchJobPostingsHandler`. No new index: the existing `IX_job_posting_owner_company_id` index already makes `owner_company_id` the query's leading, indexed column — a single Company's own row count is small enough that the trailing sort needs no dedicated index (unlike 3.3a's case, where the existing index's leading column was wrong for the query entirely).
- `IIdentityApi.GetJobSeeker(Guid) -> JobSeekerSummaryDto?` and batch `GetJobSeekers(IReadOnlyCollection<Guid>) -> IReadOnlyDictionary<Guid, JobSeekerSummaryDto>` — added to `Identity.Contracts` and implemented in `IdentityApi`, mirroring `GetCompany`/`GetCompanies` exactly (same null-on-miss / empty-map-on-empty-input contract, same projection style off `db.JobSeekerAccounts`). `JobSeekerSummaryDto { Id, FullName, Email }` — `Email` appears on this DTO and no other Contract DTO (epics.md).
- `GET /api/applications?jobPostingId=&page=&pageSize=` — Company-only (`RequireAuthorization()` + a new `Applications.Auth.CompanyOnlyEndpointFilter`, a verbatim per-module copy of `JobPostings`'s, mirroring the existing `JobSeekerOnlyEndpointFilter` duplication precedent). `jobPostingId` is a required string query param (mirrors `Applications_GetMine`'s `[Required] string?` + handler-side `Guid.TryParse` → `400` validation problem for missing/malformed). **Ownership check**: `IJobPostingsApi.GetPostingOwner(postingId)` — `null` (no such posting) or a non-null owner that doesn't equal the caller's id (someone else's posting) both yield the exact same `404` `Results.Problem` call (one `if`, one return — never a distinct `403` for "not yours"). No new precedent exists for this shape anywhere in the codebase; this story establishes it.
- `Applications` gains its first `Identity.Contracts` project reference (a new `.Contracts`-to-`.Contracts` edge — every `BoundaryRules`/`ProjectReferenceRules` check already permits any such edge unconditionally; no architecture-test changes needed).
- Applicant rows resolved via exactly one batched `IIdentityApi.GetJobSeekers` call for all distinct `job_seeker_id`s on the page — never one call per row. Ordered `SubmittedAt DESC` with an `Id` tiebreaker. Reuses `Applications`'s own `GetMyApplicationsList.Page<T>` (same module, sibling feature folder). A row whose Job Seeker is absent from the batch (data-integrity violation, unreachable in v1 — nothing deletes a Job Seeker account) is dropped from `items`, mirroring every prior orphan-drop precedent (2.3a, 3.3a); `total` is the raw match count computed before batch resolution.
- `TreatWarningsAsErrors` stays on; `dotnet build` and the full `dotnet test` (ArchitectureTests, Host.Tests, IntegrationTests) are green, including every unchanged Identity / JobPostings / Applications row.
- `cd frontend && npm run generate:api` is run after a Host build and the regenerated `frontend/src/shared/api/` committed so the CI drift gate stays green; a second run leaves `git status` clean.

**Never:**
- No new EF migration, no new database index — every query this story adds is already served by an existing index whose leading column matches the filter (`owner_company_id`, `job_posting_id`).
- No `403` anywhere in the applicant-list's ownership path — missing and not-owned are the identical `404`, byte-for-byte (RFC 9457 `type`/`title`/`status`/`detail` all the same).
- No company name, posting title, or cross-referencing beyond what each endpoint's own scope needs — `/job-postings/mine` returns posting fields only (no applicant counts); `/applications` returns applicant fields only (no posting fields — the frontend already has the posting from the page it drilled in from).
- No frontend feature code — the only frontend change is the regenerated `shared/api/`. (3.4b owns the My Postings + Applicants pages.)
- No change to any existing operation id, route, or response shape (`JobPostings_Create/GetById/Search`, `Applications_Create/GetMine/GetMyApplications`) — this story adds two new operations, touches nothing existing beyond the two `.csproj`/module-registration files and the `IIdentityApi` contract's own file.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| My postings, has some | `GET /api/job-postings/mine?page=1`, Company with 2+ postings | `200 Page<...>` with only the caller's postings, most-recent first | N/A |
| My postings, none | Company with zero postings | `200 { items: [], total: 0 }` | N/A |
| My postings clamping | `page=0` / `pageSize=0` or `>100` | clamped to `1` / `20` | N/A |
| My postings, anonymous / Job Seeker | no cookie / `job_seeker` session | `401` / `403` | N/A |
| Applicants, has some | `GET /api/applications?jobPostingId={id}` for an owned posting with 2+ applicants from different accounts | `200 Page<...>` most-recent first, each row's `fullName`/`email` correctly resolved | N/A |
| Applicants, none | owned posting, zero applicants | `200 { items: [], total: 0 }` | N/A |
| Applicants, missing posting | `jobPostingId` matches no real posting | `404` ProblemDetails | N/A |
| Applicants, another Company's posting | `jobPostingId` exists but `owner_company_id` ≠ caller | `404` ProblemDetails — **byte-identical** to the missing-posting case (same title/detail/status) | N/A |
| Applicants, malformed/missing id | blank / non-GUID `jobPostingId`, or omitted | `400` validation ProblemDetails naming `jobPostingId` | N/A |
| Applicants clamping | `page`/`pageSize` out of range | clamped to `1` / `20` | N/A |
| Applicants, anonymous / Job Seeker | no cookie / `job_seeker` session | `401` / `403` | N/A |
| Applicants, orphan applicant (unreachable in v1) | an application row whose `job_seeker_id` matches no real account | dropped from `items`; `total` still reflects the raw count | N/A |
| `IIdentityApi.GetJobSeeker(s)` | hit / miss / empty-batch | matches `GetCompany`/`GetCompanies`'s existing contract behavior exactly | N/A |
| OpenAPI document | `GET /openapi/v1.json` | `JobPostings_GetMine` and `Applications_GetApplicants` both documented with their query params and `200` schemas; every existing operation unchanged | N/A |

</frozen-after-approval>

## Code Map

**Identity — contract extension** (mirror `GetCompany`/`GetCompanies` exactly)
- `backend/NexusJob.Modules.Identity.Contracts/JobSeekerSummaryDto.cs` -- NEW. `public sealed record JobSeekerSummaryDto(Guid Id, string FullName, string Email);`
- `backend/NexusJob.Modules.Identity.Contracts/IIdentityApi.cs` -- MODIFY. Add `JobSeekerSummaryDto? GetJobSeeker(Guid id);` and `IReadOnlyDictionary<Guid, JobSeekerSummaryDto> GetJobSeekers(IReadOnlyCollection<Guid> ids);`, XML docs mirroring `GetCompany`/`GetCompanies` verbatim (single-lookup vs batch-for-list-projections framing).
- `backend/NexusJob.Modules.Identity/IdentityApi.cs` -- MODIFY. Implement both, projecting from `db.JobSeekerAccounts` (`Id`, `FullName`, `Email`) exactly as `GetCompany`/`GetCompanies` project from `db.CompanyAccounts`.
- `backend/NexusJob.IntegrationTests/ContractApiTests.cs` -- MODIFY. Add `GetJobSeeker`/`GetJobSeekers` hit / miss / empty-batch cases, mirroring the existing `GetCompany`/`GetCompanies` block.

**JobPostings — `mine` list** (`backend/NexusJob.Modules.JobPostings/Features/GetMyJobPostings/`, mirror `Features/SearchJobPostings/SearchJobPostingsHandler.cs`'s clamp/skip logic)
- `JobPostingMineItemResponse.cs` -- NEW. `public sealed record JobPostingMineItemResponse(string Id, string Title, string Description, DateTimeOffset CreatedAt);`
- `GetMyJobPostingsEndpoint.cs` -- NEW. `internal static class { Handle([FromQuery] int? page, [FromQuery] int? pageSize, [FromServices] GetMyJobPostingsHandler handler, HttpContext httpContext, CancellationToken ct) => handler.HandleAsync(page, pageSize, httpContext, ct); }`.
- `GetMyJobPostingsHandler.cs` -- NEW. `internal sealed class GetMyJobPostingsHandler(JobPostingsDbContext db)`. Resolve `ownerCompanyId` from `NameIdentifier` (401 defensive). Clamp `page`/`pageSize` (verbatim from `SearchJobPostingsHandler`). `db.JobPostings.Where(p => p.OwnerCompanyId == ownerCompanyId)`; `total` before paging; `.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip(...).Take(...)`; map to `JobPostingMineItemResponse`; return `Results.Ok(new SearchJobPostings.Page<JobPostingMineItemResponse>(items, clampedPage, clampedPageSize, total))` (the module's existing `Page<T>`, imported from the sibling feature namespace — no new `Page<T>` file).
- `backend/NexusJob.Modules.JobPostings/JobPostingsModule.cs` -- MODIFY. `AddJobPostingsModule`: register `GetMyJobPostingsHandler`. `MapJobPostingsModule`: `group.MapGet("/mine", GetMyJobPostingsEndpoint.Handle).WithName("JobPostings_GetMine").RequireAuthorization().AddEndpointFilter<CompanyOnlyEndpointFilter>().Produces<Page<JobPostingMineItemResponse>>(200).ProducesProblem(401).ProducesProblem(403);` — placed so the literal `/mine` segment is registered alongside `/{id:guid}`; `"mine"` fails the `:guid` constraint regardless of registration order, so the two never conflict (worth a build-time sanity check, not just trusting routing precedence).

**Applications — `CompanyOnlyEndpointFilter` + applicant list** (mirror `JobPostings.Auth.CompanyOnlyEndpointFilter` and `Features/GetMyApplicationsList/GetMyApplicationsListHandler.cs`)
- `backend/NexusJob.Modules.Applications/NexusJob.Modules.Applications.csproj` -- MODIFY. Add `<ProjectReference Include="..\NexusJob.Modules.Identity.Contracts\NexusJob.Modules.Identity.Contracts.csproj" />` (Applications' first Identity edge).
- `backend/NexusJob.Modules.Applications/Auth/CompanyOnlyEndpointFilter.cs` -- NEW. Verbatim per-module copy of `JobPostings.Auth.CompanyOnlyEndpointFilter` (same claim constant, same 403 title), mirroring how `JobSeekerOnlyEndpointFilter` was already duplicated the other direction in 3.1a.
- `backend/NexusJob.Modules.Applications/Features/GetApplicants/ApplicantListItemResponse.cs` -- NEW. `public sealed record ApplicantListItemResponse(string JobSeekerId, string FullName, string Email, DateTimeOffset SubmittedAt);`
- `backend/NexusJob.Modules.Applications/Features/GetApplicants/GetApplicantsEndpoint.cs` -- NEW. `internal static class { Handle([FromQuery][Required] string? jobPostingId, [FromQuery] int? page, [FromQuery] int? pageSize, [FromServices] GetApplicantsHandler handler, HttpContext httpContext, CancellationToken ct) => handler.HandleAsync(jobPostingId, page, pageSize, httpContext, ct); }`.
- `backend/NexusJob.Modules.Applications/Features/GetApplicants/GetApplicantsHandler.cs` -- NEW. `internal sealed class GetApplicantsHandler(ApplicationsDbContext db, IJobPostingsApi jobPostingsApi, IIdentityApi identityApi)`. Resolve `companyId` from `NameIdentifier` (401 defensive). `Guid.TryParse(jobPostingId)` failure → `400` `ValidationProblem` naming `jobPostingId`. `var owner = jobPostingsApi.GetPostingOwner(postingId); if (owner is null || owner != companyId) return Results.Problem(title: "This posting is no longer available.", statusCode: 404);` — one branch, byte-identical for both cases by construction. Clamp `page`/`pageSize` (verbatim constants). `db.Applications.Where(a => a.JobPostingId == postingId)`; `total` before paging; `.OrderByDescending(a => a.SubmittedAt).ThenBy(a => a.Id).Skip(...).Take(...).Select(a => new { a.JobSeekerId, a.SubmittedAt })`; batch `identityApi.GetJobSeekers(distinct seeker ids)`; map, `continue`-dropping any row absent from the batch; return `Results.Ok(new GetMyApplicationsList.Page<ApplicantListItemResponse>(items, clampedPage, clampedPageSize, total))` (the module's existing `Page<T>`).
- `backend/NexusJob.Modules.Applications/ApplicationsModule.cs` -- MODIFY. `AddApplicationsModule`: register `CompanyOnlyEndpointFilter`, `GetApplicantsHandler`. `MapApplicationsModule`: `group.MapGet("", GetApplicantsEndpoint.Handle).WithName("Applications_GetApplicants").RequireAuthorization().AddEndpointFilter<CompanyOnlyEndpointFilter>().Produces<Page<ApplicantListItemResponse>>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);` (a `GET` on the same `""` path as the existing `POST` create — different HTTP methods, no collision).

**Tests** (`backend/NexusJob.IntegrationTests/`)
- `TestSupport.cs` -- add `AuthApiClient.GetMyPostingsAsync(int? page, int? pageSize)` and `GetApplicantsAsync(string jobPostingId, int? page, int? pageSize)` (both plain Company-authenticated GETs, mirror `SearchPostingsAsync`/`GetMyApplicationsAsync`'s optional-query-string-builder pattern); `JobPostingMineItemDto`, `ApplicantListItemDto` records.
- `GetMyJobPostingsEndpointTests.cs` -- NEW. One test per My-Postings matrix row.
- `GetApplicantsEndpointTests.cs` -- NEW. One test per Applicants matrix row, including the byte-identical-404 assertion (compare the full JSON body of the missing-posting and not-owned-posting responses) and the multi-applicant/multi-company-account title-resolution test (proves batching correctness the same indirect way 2.3a/3.3a's equivalents do).
- `OpenApiDocumentTests.cs` -- add tests for `JobPostings_GetMine` and `Applications_GetApplicants`, mirroring the existing `JobPostings_Search`/`Applications_GetMyApplications` assertions.

**Frontend**
- `frontend/src/shared/api/*` -- run `npm run generate:api` after a Host build; commit. NSwag adds `JobPostingsClient.getMine(page?, pageSize?)` and `ApplicationsClient.getApplicants(jobPostingId, page?, pageSize?)` plus their response types; every existing method/type is unchanged. No hand edits; a second run is drift-clean.

**Reference — do not change**
- `backend/NexusJob.Modules.JobPostings/Features/SearchJobPostings/{Page.cs,SearchJobPostingsHandler.cs}` -- the `Page<T>` reused by `GetMyJobPostingsHandler` and the clamp/skip logic mirrored by both new handlers.
- `backend/NexusJob.Modules.Applications/Features/GetMyApplicationsList/{Page.cs,GetMyApplicationsListHandler.cs}` -- the `Page<T>` reused by `GetApplicantsHandler` and the batched-resolution-with-orphan-drop pattern mirrored.
- `backend/NexusJob.Modules.Identity.Contracts/{IIdentityApi.cs,CompanySummaryDto.cs}`, `backend/NexusJob.Modules.Identity/IdentityApi.cs` -- the exact `GetCompany`/`GetCompanies` shape `GetJobSeeker`/`GetJobSeekers` mirrors.
- `backend/NexusJob.Modules.JobPostings/Auth/CompanyOnlyEndpointFilter.cs` -- the filter `Applications`'s new copy mirrors verbatim.
- `backend/NexusJob.Modules.JobPostings.Contracts/IJobPostingsApi.cs` -- `GetPostingOwner` is the existence/ownership check; already published, do not modify.
- `_bmad-output/implementation-artifacts/epic-3-context.md` -- the 404-never-403 ownership decision and the batched-resolution requirement, both already specified there.

## Tasks & Acceptance

**Execution:**
- [x] `backend/NexusJob.Modules.Identity.Contracts/JobSeekerSummaryDto.cs` + `IIdentityApi.cs` -- the new contract members.
- [x] `backend/NexusJob.Modules.Identity/IdentityApi.cs` -- `GetJobSeeker`/`GetJobSeekers` implementations.
- [x] `backend/NexusJob.Modules.JobPostings/Features/GetMyJobPostings/{JobPostingMineItemResponse,GetMyJobPostingsEndpoint,GetMyJobPostingsHandler}.cs` -- the Company-scoped postings list.
- [x] `backend/NexusJob.Modules.JobPostings/JobPostingsModule.cs` -- register the handler; map `GET /mine` (`JobPostings_GetMine`).
- [x] `backend/NexusJob.Modules.Applications/NexusJob.Modules.Applications.csproj` -- add the `Identity.Contracts` reference.
- [x] `backend/NexusJob.Modules.Applications/Auth/CompanyOnlyEndpointFilter.cs` -- the new per-module filter.
- [x] `backend/NexusJob.Modules.Applications/Features/GetApplicants/{ApplicantListItemResponse,GetApplicantsEndpoint,GetApplicantsHandler}.cs` -- the owner-scoped applicant list (404-never-403, batched resolution, orphan-drop).
- [x] `backend/NexusJob.Modules.Applications/ApplicationsModule.cs` -- register the filter + handler; map `GET ""` (`Applications_GetApplicants`).
- [x] `backend/NexusJob.IntegrationTests/TestSupport.cs` -- the two new `AuthApiClient` methods + DTOs.
- [x] `backend/NexusJob.IntegrationTests/{GetMyJobPostingsEndpointTests,GetApplicantsEndpointTests}.cs` -- one test per matrix row.
- [x] `backend/NexusJob.IntegrationTests/ContractApiTests.cs` -- `GetJobSeeker`/`GetJobSeekers` cases.
- [x] `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` -- the two new operations.
- [x] `frontend/src/shared/api/*` -- `npm run generate:api`; commit; verify drift-clean.

**Acceptance Criteria:**
- Given a Company with postings from itself and another Company exists in the system, when `GET /api/job-postings/mine` is called, then only the caller's own postings are returned, most-recent first.
- Given a posting the caller owns with applicants from two different Job Seeker accounts, when `GET /api/applications?jobPostingId={id}` is called, then each row's `fullName`/`email` is correctly resolved via a single batched call and rows are most-recent first.
- Given a `jobPostingId` that does not exist, and separately one that exists but is owned by a different Company, when `GET /api/applications?jobPostingId=` is called for each, then both responses are `404` ProblemDetails with byte-for-byte identical bodies.
- Given `dotnet build backend/NexusJob.sln` and `dotnet test backend/NexusJob.sln`, when they run, then the build is warning-free and every suite passes — the boundary/raw-SQL/registry gates, every unchanged Identity/JobPostings/Applications row, and the new tests.
- Given a Host build then `cd frontend && npm run generate:api`, when it runs, then `frontend/src/shared/api/` gains `getMine`/`getApplicants`, every existing method is unchanged, a second run leaves `git status` clean, and `npm run lint`/`npm test`/`npm run build` pass.

## Implementation Notes

Independently re-verified after the implementation subagent's report (step 03):
- Read `GetApplicantsHandler.cs`, `GetMyJobPostingsHandler.cs`, `IdentityApi.cs`'s `GetJobSeeker`/`GetJobSeekers`, `CompanyOnlyEndpointFilter.cs`, and `GetApplicantsEndpoint.cs` directly — all match the frozen spec, including the single-branch byte-identical 404 (`owner is null || owner != companyId`), in-module `Page<T>` reuse, and verbatim-copied clamp/skip arithmetic.
- `dotnet build backend/NexusJob.sln -c Release`: 0 warnings, 0 errors.
- `dotnet test backend/NexusJob.sln -c Release --no-build`: ArchitectureTests 22/22, Host.Tests 3/3, IntegrationTests 147/147 — all green, matching the subagent's report.
- Frontend: `npm run lint` clean; `test:fsd-gate` / `test:api-import-gate` / `test:tokens-gate` all pass (each rejects its fixture violation as expected); `npx vitest run` 266/266 across 28 files; `npm run build` clean.
- `npm run generate:api` re-run against the built Host: produced an empty diff against the already-staged `nexus-api-client.ts` — confirms the committed client is byte-for-byte drift-free.
- Matrix Test Audit: all 14 I/O-matrix rows map to at least one test — my-postings has-some/none/clamping(4 tests)/401+403 in `GetMyJobPostingsEndpointTests.cs`; applicants has-some/none/missing+other-company-404(one combined byte-identical-body test)/malformed+omitted-id-400/clamping/401+403/orphan-drop in `GetApplicantsEndpointTests.cs`; `IIdentityApi.GetJobSeeker`/`GetJobSeekers` hit/miss/empty-batch in `ContractApiTests.cs`; both new OpenAPI operations (params, response codes, `problem+json` 4xx, unchanged sibling operations) in `OpenApiDocumentTests.cs`.
- Confirmed the flagged deviation (`AuthApiClient.GetApplicantsAsync`'s `jobPostingId` as nullable `string?` rather than the spec's literal non-nullable `string`) is intentional and correct: it's the only way to drive the omitted-parameter matrix row through the typed test client, mirroring `GetMyApplicationAsync`'s established pattern; the wire contract (`[Required] string?` at the endpoint) is unaffected.
- No corrections needed; implementation accepted as delivered.

## Spec Change Log

## Review Triage Log

Three parallel review subagents ran against the diff (blind-hunter, edge-case-hunter, verification-gap).

**blind-hunter:** NO FINDINGS. Independently re-verified the 404-never-403 branch, clamp/skip arithmetic vs. `SearchJobPostingsHandler`, route registration (`/mine` vs `/{id:guid}`, `GET ""` vs `POST ""`), the new `Identity.Contracts` project reference, and the generated frontend client — no correctness/security/maintainability defects.

**edge-case-hunter** and **verification-gap** each independently surfaced the same two real gaps, plus verification-gap raised two further items:

1. **PATCHED — Missing "blank" `jobPostingId` test.** The frozen matrix row lists "blank / non-GUID / omitted" but only non-GUID and omitted were tested; `jobPostingId=` (present, binds to `""`) is a distinct wire case. Added `Blank_job_posting_id_returns_400_validation_problem_naming_the_field` to `GetApplicantsEndpointTests.cs`.
2. **PATCHED — Byte-identical-404 test wasn't a true full-body comparison, and its "traceId" exclusion rationale needed verification.** The test only compared 4 named `ProblemBody` fields via a typed DTO. Ran the test with a genuine raw-string comparison first to check the premise empirically: the two bodies do differ in exactly one field, `traceId` (stamped per-request by the framework's default `ProblemDetailsService` even with no `CustomizeProblemDetails` callback registered — confirmed by the failing assertion showing two different `traceId` values and nothing else). Replaced the 4-field comparison with a full-body JSON comparison that excludes only `traceId` (`WithoutTraceId` helper: parses, drops that one property, re-serializes in a fixed key order), so any other field's divergence would now be caught. Updated the stale comment accordingly.
3. **PATCHED — Clamping tests never hit the exact upper boundary (101).** Both `GetApplicantsEndpointTests.cs` and `GetMyJobPostingsEndpointTests.cs`'s `Out_of_range_page_size_is_clamped_to_the_default` theories tested `0` and `500` but not `101`, the one value that actually pins the `> MaxPageSize` cutoff (a `>=`-vs-`>` or off-by-one regression in `MaxPageSize` itself would pass undetected). Added `[InlineData(101)]` to both theories.
4. **DEFERRED — No test asserts `IIdentityApi.GetJobSeekers` is called exactly once (not per-row).** The spec's Boundaries & Constraints makes this behavioral claim, but proving it needs a call-count spy on `IIdentityApi`, which no existing test in the codebase does either (2.3a's `GetCompanies`/3.3a's equivalent batched calls have the same limitation) — the spec itself flags this as proven only "the same indirect way 2.3a/3.3a's equivalents do." Not a regression introduced by this story; deferred as pre-existing, consistent-with-precedent test-suite debt rather than patched here.

Post-patch: full re-run confirms `dotnet build` 0W/0E; `dotnet test` 22/22 (Architecture) + 3/3 (Host) + 150/150 (Integration, +3 over the pre-review 147).

## Design Notes

- **Why the byte-identical 404 is one branch, not two.** Writing `if (owner is null) return NotFound(); if (owner != companyId) return NotFound();` as two separate statements risks the two responses drifting apart (a future edit to one message and not the other). One `if (owner is null || owner != companyId)` with a single `Results.Problem` call makes the identity structurally guaranteed, not just coincidentally true today.
- **Reusing each module's own existing `Page<T>` instead of a third copy.** The "no shared kernel" Design Note from 2.3a/3.1a is about crossing a *module* boundary; `SearchJobPostings.Page<T>` and `GetMyApplicationsList.Page<T>` are already reachable from a sibling feature folder in the *same* module/assembly, so reusing them is a plain in-module reference, not a new cross-module dependency — duplicating a third one would just be redundant.
- **No new indexes.** Both new queries filter on a column that is already the leading (and in one case sole) column of an existing index (`owner_company_id`, `job_posting_id`); unlike 3.3a's `job_seeker_id` case, the existing indexes already serve these access patterns.

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings, 0 errors.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass, including the new test files and every unchanged suite (Docker required for Testcontainers `postgres:18`).
- `cd frontend && npm ci && npm run generate:api && git status --porcelain src/shared/api` -- expected: gains `getMine`/`getApplicants` on the first run; empty on a re-run.
- `npm run lint && npm test -- --run && npm run build` -- expected: all green with the regenerated client.

**Manual checks:**
- `docker compose up`; as a Company with two postings, `GET /api/job-postings/mine` shows both, not a different Company's. Apply to one posting as two different Job Seekers, then `GET /api/applications?jobPostingId=` as the owning Company shows both applicants with correct names/emails. As a different Company, the same call returns `404`.
- `psql`: no schema change — `applications` and `job_postings` schemas are byte-identical to before this story.
