---
title: 'IIdentityApi / IJobPostingsApi contracts and GET /api/job-postings/{id}'
type: 'feature'
created: '2026-09-09'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'dde98f35eb6ff89fe96ba38a0cc27aa2b6898c8d'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** JobPostings can create a posting but nothing can read one back. There is no `GET /api/job-postings/{id}`, and the cross-module Contract a detail view needs to show the posting Company's name — `IIdentityApi` — does not exist (both `.Contracts` projects hold only an assembly-marker class). `IJobPostingsApi`, which Epic 3 consumers will use, is likewise unpublished.

**Approach:** Publish `IIdentityApi` (+ `CompanySummaryDto`) from `NexusJob.Modules.Identity.Contracts` and implement it as a synchronous in-process service over `IdentityDbContext`, registered in `AddIdentityModule`. Publish `IJobPostingsApi` (+ `JobPostingSummaryDto`) from `NexusJob.Modules.JobPostings.Contracts` and implement it over `JobPostingsDbContext`. Add an anonymous `GET /api/job-postings/{id:guid}` slice whose handler reads the posting and resolves the owner Company's display name through `IIdentityApi.GetCompany` — never a query against the `identity` schema — returning `200 { id, title, description, companyName }` or a `404` ProblemDetails when no row matches. Regenerate the committed OpenAPI client. This is story **2.2a**; the detail surface and the publish→detail redirect are **2.2b** (`deferred-work.md`).

## Boundaries & Constraints

**Always:**
- Architecture ADs as the earlier JobPostings/Identity stories applied them. **AD-2 / AD-19:** Contracts expose named DTOs only (never entities), `Guid` ids in C# signatures (string only at the JSON edge), fields non-null unless the name says otherwise; `IIdentityApi` / `IJobPostingsApi` are synchronous in-process services; the only new project edge is `NexusJob.Modules.JobPostings → NexusJob.Modules.Identity.Contracts` (a `.Contracts` reference — the ArchUnitNET boundary gates stay green with no test edit; `IsContracts` already permits any `*.Contracts` reference). **AD-5:** the detail handler resolves the Company name only through `IIdentityApi`; no cross-schema SQL, no reference to any Identity implementation type. **AD-14:** endpoint delegate calls a slice handler resolved from DI; no mediator. **AD-15 / AD-18:** RFC 9457 ProblemDetails on every non-2xx; `200` returns the representation directly, no envelope; one OpenAPI 3.0 document regenerates `frontend/src/shared/api`. **AD-18 §detail:** `GET /api/job-postings/{id}` is anonymous (`.AllowAnonymous()`) — no antiforgery, no auth filter.
- `GET /api/job-postings/{id:guid}` → `200 { id, title, description, companyName }` for an existing posting; the `{id:guid}` route constraint makes any non-GUID segment a routing `404`; a well-formed id with no row is a `404` `application/problem+json` from the handler.
- `IIdentityApi.GetCompany(Guid) → CompanySummaryDto?` (null = no such company) and batch `GetCompanies(IReadOnlyCollection<Guid>) → IReadOnlyDictionary<Guid, CompanySummaryDto>` (missing ids absent from the map); `CompanySummaryDto { Guid Id; string DisplayName }` exactly per AD-19. Implemented over `IdentityDbContext` with projection queries mirroring `GetMeHandler` (`DisplayName` from `CompanyAccount`), registered `services.AddScoped<IIdentityApi, IdentityApi>()` in `AddIdentityModule`.
- `IJobPostingsApi.GetPostingOwner(Guid) → Guid?` (null = no such posting) and batch `GetPostingSummaries(IReadOnlyCollection<Guid>) → IReadOnlyDictionary<Guid, JobPostingSummaryDto>`. `JobPostingSummaryDto { Guid Id; string Title; string Description; Guid OwnerCompanyId; DateTimeOffset CreatedAt }` — posting-owned fields only; a consumer that needs the Company name calls `IIdentityApi.GetCompanies` itself (AD-19 batch-compose), so `JobPostingsApi` takes no `IIdentityApi` dependency. Registered `services.AddScoped<IJobPostingsApi, JobPostingsApi>()` in `AddJobPostingsModule`.
- `TreatWarningsAsErrors` stays on; `dotnet build` and the full `dotnet test` (ArchitectureTests, Host.Tests, IntegrationTests) are green, including the unchanged 2.1a `JobPostingsEndpointsTests` and every Identity / Architecture row. Every I/O-matrix row has an integration test against real Postgres (Testcontainers `postgres:18`, the existing `IdentityApiCollection` fixture — do not add a fixture).
- `cd frontend && npm run generate:api` is run and the regenerated `frontend/src/shared/api/` committed so the CI `openapi-client` drift gate stays green; a second run leaves `git status` clean. `npm run lint` / `npm test` / `npm run build` stay green (the generated `getById` client method is `eslint`-ignored, still `tsc`-checked).

**Never:**
- No frontend feature code — no `pages/*`, no `entities/job-posting` detail query, no route, no nav change, no change to `features/create-posting`'s success path. The only frontend change is the regenerated `shared/api/`. (2.2b owns the surface and the publish→detail redirect.)
- No search endpoint (`GET /api/job-postings?query=…` is 2.3), no `Page<T>` type, no `IJobPostingsApi` *consumer* — nothing calls `GetPostingSummaries` / `GetPostingOwner` yet (publish only).
- No `GetJobSeeker` / `JobSeekerSummaryDto` on `IIdentityApi` (AD-19 lists them, but only FR-7 / Epic 3 needs them — add with their consumer).
- No new EF migration, no schema change, no new column, no data mutation — 2.2a is read-only over existing tables.
- No `Host/Program.cs` change: `AddIdentityModule` / `AddJobPostingsModule` / `MapJobPostingsModule` are already wired, and `AddIdentityModule` runs first, so `IIdentityApi` is registered before JobPostings' handler resolves it.
- No antiforgery, no `RequireAuthorization`, no `CompanyOnlyEndpointFilter` on the detail route.
- No shared-kernel extraction; no change to the 2.1a create slice or its tests beyond what a green build needs (expected: none).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Read an existing posting, anonymous | `GET /api/job-postings/{id}`, valid id of a real row, **no auth cookie** | `200 { id, title, description, companyName }` (no envelope); `companyName` = the owning Company's `DisplayName`, obtained via `IIdentityApi.GetCompany`; no `identity`-schema query from JobPostings | N/A |
| Read an existing posting, authenticated | same, with any valid session cookie (Company or Job Seeker) | identical `200` — the endpoint is anonymous, the session is ignored | N/A |
| Well-formed id, no such posting | `GET /api/job-postings/{random guid}` | `404` `application/problem+json` (RFC 9457) | `Results.Problem(statusCode: 404)` |
| Non-GUID id segment | `GET /api/job-postings/not-a-guid` | `404` — the `{id:guid}` route constraint fails to match | routing 404 |
| Owner Company cannot be resolved | posting row exists; `IIdentityApi.GetCompany(ownerCompanyId)` returns null (data-integrity violation — unreachable in v1, no company deletion) | `500` — surfaces the inconsistency, no masking (see Design Notes) | handler throws → ProblemDetails 500 |
| `IIdentityApi.GetCompany` — hit / miss | a known company id / an unknown id | `CompanySummaryDto { Id, DisplayName }` / `null` | N/A |
| `IIdentityApi.GetCompanies` batch | a mix of known and unknown ids (and an empty collection) | `IReadOnlyDictionary` with entries for the known ids only, keyed by `Id`; empty input → empty map | N/A |
| `IJobPostingsApi.GetPostingOwner` — hit / miss | a real posting id / an unknown id | the `OwnerCompanyId` `Guid` / `null` | N/A |
| `IJobPostingsApi.GetPostingSummaries` batch | a mix of known and unknown posting ids (and an empty collection) | `IReadOnlyDictionary` with entries for the known ids only; empty input → empty map | N/A |
| OpenAPI document | `GET /openapi/v1.json` on the running Host | `paths["/api/job-postings/{id}"]` has a `get` op `JobPostings_GetById` with the `200 {id,title,description,companyName}` schema and a `404` problem response | N/A |
| Structured logs for a detail read | any `GET /api/job-postings/{id}` | one JSON log line (method / path / status / duration); no cookie or body content | N/A |

</frozen-after-approval>

## Code Map

**Backend — Identity Contract (publish + implement)**
- `backend/NexusJob.Modules.Identity.Contracts/IIdentityApi.cs` -- NEW. `public interface IIdentityApi` with `CompanySummaryDto? GetCompany(Guid id)` and `IReadOnlyDictionary<Guid, CompanySummaryDto> GetCompanies(IReadOnlyCollection<Guid> ids)`. Namespace `NexusJob.Modules.Identity.Contracts`. XML doc: synchronous in-process, null semantics, batch getter for list projections (AD-19).
- `backend/NexusJob.Modules.Identity.Contracts/CompanySummaryDto.cs` -- NEW. `public sealed record CompanySummaryDto(Guid Id, string DisplayName)` (AD-19 exact shape). No csproj change (interface + record only).
- `backend/NexusJob.Modules.Identity/IdentityApi.cs` -- NEW. `internal sealed class IdentityApi(IdentityDbContext db) : IIdentityApi`. `GetCompany`: `db.CompanyAccounts.Where(a => a.Id == id).Select(a => new CompanySummaryDto(a.Id, a.DisplayName)).SingleOrDefault()`. `GetCompanies`: `db.CompanyAccounts.Where(a => ids.Contains(a.Id)).Select(a => new CompanySummaryDto(a.Id, a.DisplayName)).ToDictionary(x => x.Id)` returned as `IReadOnlyDictionary`; empty `ids` → empty dict (no DB hit). Sync EF, mirroring `GetMeHandler`'s projection.
- `backend/NexusJob.Modules.Identity/IdentityModule.cs` -- `AddIdentityModule`: add `services.AddScoped<IIdentityApi, IdentityApi>();` after the handler registrations. `MapIdentityModule` untouched.

**Backend — JobPostings Contract (publish + implement)**
- `backend/NexusJob.Modules.JobPostings.Contracts/IJobPostingsApi.cs` -- NEW. `public interface IJobPostingsApi` with `Guid? GetPostingOwner(Guid postingId)` and `IReadOnlyDictionary<Guid, JobPostingSummaryDto> GetPostingSummaries(IReadOnlyCollection<Guid> postingIds)`. Namespace `NexusJob.Modules.JobPostings.Contracts`.
- `backend/NexusJob.Modules.JobPostings.Contracts/JobPostingSummaryDto.cs` -- NEW. `public sealed record JobPostingSummaryDto(Guid Id, string Title, string Description, Guid OwnerCompanyId, DateTimeOffset CreatedAt);` — posting-owned fields only, no company name.
- `backend/NexusJob.Modules.JobPostings/JobPostingsApi.cs` -- NEW. `internal sealed class JobPostingsApi(JobPostingsDbContext db) : IJobPostingsApi`. `GetPostingOwner`: `db.JobPostings.Where(p => p.Id == postingId).Select(p => (Guid?)p.OwnerCompanyId).SingleOrDefault()`. `GetPostingSummaries`: `Where(p => postingIds.Contains(p.Id)).Select(p => new JobPostingSummaryDto(...)).ToDictionary(x => x.Id)`; empty input → empty dict.
- `backend/NexusJob.Modules.JobPostings/JobPostingsModule.cs` -- `AddJobPostingsModule`: add `services.AddScoped<IJobPostingsApi, JobPostingsApi>();` and `services.AddScoped<GetJobPostingByIdHandler>();`. `MapJobPostingsModule`: on the existing `group`, add `group.MapGet("/{id:guid}", GetJobPostingByIdEndpoint.Handle).WithName("JobPostings_GetById").AllowAnonymous().Produces<JobPostingDetailResponse>(StatusCodes.Status200OK).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status500InternalServerError);` (the 500 is a deliberate `Results.Problem` for the orphan-owner case, so it is documented — review pass 1 patch). Update the filter-order comment to note the anonymous GET.
- `backend/NexusJob.Modules.JobPostings/NexusJob.Modules.JobPostings.csproj` -- add `<ProjectReference Include="..\NexusJob.Modules.Identity.Contracts\NexusJob.Modules.Identity.Contracts.csproj" />`.

**Backend — the detail slice** (`backend/NexusJob.Modules.JobPostings/Features/GetJobPostingById/`)
- `GetJobPostingByIdEndpoint.cs` -- NEW. `internal static class`; `public static Task<IResult> Handle([FromRoute] Guid id, [FromServices] GetJobPostingByIdHandler handler, CancellationToken cancellationToken) => handler.HandleAsync(id, cancellationToken);` (mirror `CreateJobPostingEndpoint`).
- `GetJobPostingByIdHandler.cs` -- NEW. `internal sealed class GetJobPostingByIdHandler(JobPostingsDbContext db, IIdentityApi identityApi)`. `HandleAsync(Guid id, CancellationToken ct)`: `var row = await db.JobPostings.Where(p => p.Id == id).Select(p => new { p.Id, p.Title, p.Description, p.OwnerCompanyId }).SingleOrDefaultAsync(ct);` → `if (row is null) return Results.Problem(title: "This posting is no longer available.", statusCode: StatusCodes.Status404NotFound);` → `var company = identityApi.GetCompany(row.OwnerCompanyId);` → `if (company is null) return Results.Problem(title: "The job posting could not be loaded.", statusCode: StatusCodes.Status500InternalServerError);` (a real RFC 9457 500 — user decision, see Implementation Notes) → `return Results.Ok(new JobPostingDetailResponse(row.Id.ToString(), row.Title, row.Description, company.DisplayName));`
- `JobPostingDetailResponse.cs` -- NEW. `public sealed record JobPostingDetailResponse(string Id, string Title, string Description, string CompanyName);` Serialises `{ id, title, description, companyName }`.

**Backend — tests** (`backend/NexusJob.IntegrationTests/`)
- `TestSupport.cs` -- add `AuthApiClient.GetPostingAsync(Guid id)` / `GetPostingRawAsync(string)` → anonymous `GET /api/job-postings/{id}` (no CSRF seed); add `JobPostingsDatabase.InsertPostingAsync(ownerCompanyId, title, description)` (direct insert, for the orphan-owner row). Add an `IdentityApiFixture.Services` accessor (root `IServiceProvider`) for the contract tests.
- `JobPostingDetailEndpointTests.cs` -- NEW. `[Collection(nameof(IdentityApiCollection))]`. One `[Fact]` per I/O-matrix row: anonymous happy path (`200`, body keys exactly `{companyName,description,id,title}`, `companyName` == the registered `DisplayName`, `id` echoes the created id); a signed-in Job Seeker gets the identical `200`; unknown guid → `404` `application/problem+json`; `not-a-guid` segment → `404`; an orphan posting (owner id with no `company_account`, via `InsertPostingAsync`) → `500` `application/problem+json`; the structured-log line names `/api/job-postings/` and carries no title/description/cookie. Set-up: `client.RegisterAsync("Acme Inc.", NewEmail(), Password)` then `client.CreatePostingAsync(title, description)`, parse `id` from the `200` create body.
- `ContractApiTests.cs` -- NEW. `[Collection(nameof(IdentityApiCollection))]`. Resolve `IIdentityApi` and `IJobPostingsApi` from a scope off `fixture.Services`. Pin: `GetCompany` hit (returns the registered `DisplayName`) / miss (`null`); `GetCompanies` returns only the known ids keyed by `Id`, empty input → empty; `GetPostingOwner` hit (returns the creating Company's id) / miss (`null`); `GetPostingSummaries` returns only known posting ids, empty input → empty. Sole coverage for the batch getters and all of `IJobPostingsApi`.
- `OpenApiDocumentTests.cs` -- extend: add `Document_describes_the_job_postings_get_by_id_operation_with_its_200_shape_and_404` (or a tuple to an expected-ops list) asserting `paths["/api/job-postings/{id}"].get`, `operationId` `JobPostings_GetById`, `200` schema property set `{companyName,description,id,title}`, and `404` documented as `application/problem+json`.

**Frontend**
- `frontend/src/shared/api/*` -- run `npm run generate:api` after a Host build; commit the regenerated client. NSwag adds `JobPostingsClient.getById(id)` returning a new `JobPostingDetailResponse` interface. No hand edits; a second run is drift-clean.

**Reference — do not change**
- `backend/NexusJob.Modules.JobPostings/Features/CreateJobPosting/**` -- the slice shape being mirrored; behaviour unchanged.
- `backend/NexusJob.Modules.Identity/Features/GetMe/GetMeHandler.cs` -- the `CompanyAccount.DisplayName` projection `IdentityApi.GetCompany` mirrors.
- `backend/NexusJob.ArchitectureTests/**` -- must stay green with the new `JobPostings → Identity.Contracts` edge; `IsContracts` already permits any `*.Contracts` reference, so no test edit is expected (verify).
- `backend/NexusJob.IntegrationTests/IdentityApiFixture.cs` / `IdentityApiCollection.cs` -- reuse as-is (only a `Services` accessor may be added).
- `_bmad-output/implementation-artifacts/spec-2-1a-job-postings-create-endpoint.md` -- continuity: the `MapGroup` / slice / `WithName` conventions, the `IdentityApiCollection` fixture + `AuthApiClient` helpers, the `OpenApiDocumentTests` assertion pattern, the "no envelope / `Results.Ok`" response convention.

## Tasks & Acceptance

**Execution:**
- [x] `backend/NexusJob.Modules.Identity.Contracts/{IIdentityApi,CompanySummaryDto}.cs` -- publish the interface + DTO (AD-19 exact shape, synchronous, nullable `GetCompany`, batch `GetCompanies`).
- [x] `backend/NexusJob.Modules.Identity/IdentityApi.cs` + `IdentityModule.cs` -- `internal sealed IdentityApi : IIdentityApi` over `IdentityDbContext` (projection queries mirroring `GetMeHandler`); register `AddScoped<IIdentityApi, IdentityApi>()` in `AddIdentityModule`.
- [x] `backend/NexusJob.Modules.JobPostings.Contracts/{IJobPostingsApi,JobPostingSummaryDto}.cs` -- publish the interface + DTO (`{ Id, Title, Description, OwnerCompanyId, CreatedAt }`, posting-owned only).
- [x] `backend/NexusJob.Modules.JobPostings/JobPostingsApi.cs` + `NexusJob.Modules.JobPostings.csproj` -- `internal sealed JobPostingsApi : IJobPostingsApi` over `JobPostingsDbContext`; add the `Identity.Contracts` project reference.
- [x] `backend/NexusJob.Modules.JobPostings/Features/GetJobPostingById/{GetJobPostingByIdEndpoint,GetJobPostingByIdHandler,JobPostingDetailResponse}.cs` -- the anonymous delegate, the handler (posting projection → `404` when absent → `IIdentityApi.GetCompany` → `200`), the response record.
- [x] `backend/NexusJob.Modules.JobPostings/JobPostingsModule.cs` -- register `GetJobPostingByIdHandler` + `IJobPostingsApi`; map `GET /{id:guid}` (`JobPostings_GetById`, `.AllowAnonymous()`, `.Produces<JobPostingDetailResponse>(200)`, `.ProducesProblem(404)`).
- [x] `backend/NexusJob.IntegrationTests/TestSupport.cs` -- `AuthApiClient.GetPostingAsync(Guid id)` (anonymous GET); `IdentityApiFixture.Services` accessor if absent.
- [x] `backend/NexusJob.IntegrationTests/JobPostingDetailEndpointTests.cs` -- one test per I/O-matrix row (anonymous + authenticated `200`, unknown-guid `404`, non-guid `404`, log redaction).
- [x] `backend/NexusJob.IntegrationTests/ContractApiTests.cs` -- resolve both contract services from DI; pin `GetCompany` / `GetCompanies` / `GetPostingOwner` / `GetPostingSummaries` hit / miss / empty.
- [x] `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` -- assert `GET /api/job-postings/{id}` `JobPostings_GetById` with its `200` shape + `404` problem response.
- [x] `frontend/src/shared/api/*` -- `npm run generate:api` after a Host build; commit; verify drift-clean on re-run.

**Acceptance Criteria:**
- Given `NexusJob.Modules.Identity.Contracts`, when the build completes, then it exposes `IIdentityApi` with `GetCompany(Guid)` and the batch `GetCompanies(IReadOnlyCollection<Guid>)` plus `CompanySummaryDto { Guid Id; string DisplayName }`, references no implementation project, and `GetRequiredService<IIdentityApi>()` resolves in the running Host.
- Given `NexusJob.Modules.JobPostings.Contracts`, when the build completes, then it exposes `IJobPostingsApi` with `GetPostingOwner(Guid) → Guid?` and `GetPostingSummaries(IReadOnlyCollection<Guid>) → IReadOnlyDictionary<Guid, JobPostingSummaryDto>`, and `NexusJob.Modules.JobPostings` references `NexusJob.Modules.Identity.Contracts` with every ArchUnitNET boundary test still green.
- Given a posting created via `POST /api/job-postings` by a Company registered as "Acme Inc.", when `GET /api/job-postings/{id}` is called with no session, then the response is `200 { id, title, description, companyName: "Acme Inc." }` and JobPostings issued no query against the `identity` schema (the name came from `IIdentityApi`).
- Given `dotnet build backend/NexusJob.sln` and `dotnet test backend/NexusJob.sln`, when they run, then the build is warning-free and every suite passes — the boundary / raw-SQL gates, the unchanged 2.1a `JobPostingsEndpointsTests` and Identity / Host suites, and the new detail + contract + OpenAPI tests.
- Given a Host build then `cd frontend && npm run generate:api`, when it runs, then `frontend/src/shared/api/` gains `JobPostingsClient.getById` + `JobPostingDetailResponse`, a second run leaves `git status` clean, and `npm run lint` / `npm test` / `npm run build` pass.

## Implementation Notes

- **2026-09-09 — row 5 (`owner Company unresolvable`) made a real ProblemDetails 500.** The first implementation had `GetJobPostingByIdHandler` `throw new InvalidOperationException` for the null-company branch. The Host has `AddProblemDetails()` but no `UseExceptionHandler` (and the spec's "Never" list forbids a `Program.cs` change), so a throw yields a bare 500, not the `application/problem+json` the frozen I/O matrix row states. Per user decision at the step-03 matrix audit, the handler now returns `Results.Problem(statusCode: 500, title: "The job posting could not be loaded.")` — an explicit `IResult`, so a genuine RFC 9457 body with no Host change. The frozen row's "handler throws" wording describes the old mechanism; the binding outcome ("ProblemDetails 500, no masking") is now literally met. Added `JobPostingsDatabase.InsertPostingAsync` + an orphan-posting test.

- **2026-09-09 — review pass 1 patches (3, all `low`).** (1) `GET /{id:guid}` now also declares `.ProducesProblem(500)` — the handler deliberately returns a `Results.Problem(500)`, so the OpenAPI document and the regenerated `getById` client now carry that response (an earlier same-session edit had left it at `200` + `404` "matching `Create`"; a deliberate `Results.Problem` outranks that). (2) Dropped the unreachable `ids is null` half of the guard in `IdentityApi.GetCompanies` / `JobPostingsApi.GetPostingSummaries` (non-nullable param) and the "or null" wording in the `.Contracts` docs. (3) The non-GUID-segment test now also asserts the 404 is not `application/problem+json` (a bare routing 404). Full re-verification green: build 0W/0E, `dotnet test` 22 + 3 + 68, frontend lint / 121 tests / build, `generate:api` drift-clean.

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-09) — blind-hunter, edge-case-hunter, verification-gap

Local re-verification before the pass was green: `dotnet build` 0W/0E, `dotnet test` = Architecture 22, Host 3, IntegrationTests 68; `generate:api` drift-clean. No `intent_gap`, no `bad_spec` — no loopback. 3 `patch`, 1 `defer`, 12 rejected.

**patch:**
- `JobPostingsModule.cs` GET mapping (blind-hunter) — `low`. Documents `.Produces(200)` + `.ProducesProblem(404)` only, but the handler deliberately returns `Results.Problem(500)` for the orphan-owner case, so the OpenAPI doc omits a response the endpoint really produces and the generated `processGetById` has no `500` branch. Fix: add `.ProducesProblem(500)`, regenerate the client. (This re-adds the line removed earlier this session under "match Create's convention" — a deliberate `Results.Problem` outranks that convention.)
- `IdentityApi.cs` / `JobPostingsApi.cs` + the two `.Contracts` interface docs (blind-hunter + verification-gap "other") — `low`, one root cause. Batch getters take a non-nullable `IReadOnlyCollection<Guid>` but guard `if (ids is null || …)` and their XML docs promise "empty or null" handling — a null path the type system (NRT + `TreatWarningsAsErrors`) disallows and no test exercises. Fix: drop the `is null` half of each guard (keep `.Count == 0`), remove "or null" from all four doc comments.
- `JobPostingDetailEndpointTests.Get_posting_by_id_with_a_non_guid_segment_returns_404_from_routing` (blind-hunter) — `low`. Asserts only the 404 status, so it would not catch a regression where a non-GUID id reaches the handler and 404s there instead of at the `{id:guid}` route constraint. Fix: also assert the response is not `application/problem+json` (a bare routing/fallback 404).

**defer:**
- No global exception handler (edge-case-hunter E1) — `low`, pre-existing and app-wide. The Host registers `AddProblemDetails()` but no `UseExceptionHandler`, so an unhandled exception from any handler (a transient Npgsql error in `SingleOrDefaultAsync`, or from `IdentityApi.GetCompany`) yields a bare 500, not RFC 9457. `GetJobPostingByIdHandler` is no worse than `CreateJobPostingHandler` here. → `deferred-work.md`.

**rejected:**
- No `ILogger` on the data-integrity 500 branch (blind-hunter) — `low`. Direct 2.1a precedent: review pass 1 there ruled a log on a can't-happen defensive branch is noise, and no `Features/*` handler in either module injects a logger. The orphan-owner path is unreachable in v1.
- 404 title "This posting is no longer available." is misleading for a never-existed id (blind-hunter) — `low`. It is the exact detail-surface copy epics.md Story 2.2 prescribes, so 2.2b can surface `problem.title` directly; RFC 9457 `title` is advisory (consumers key on `status`/`type`).
- Duplicate-id batch input untested; "`ToDictionary` throws on dup keys" (blind-hunter) — `false`. `ToDictionary` consumes the EF query result, not the caller's `ids`; `Where(p => ids.Contains(p.Id))` over a PK column yields ≤1 row per id, so duplicate input ids collapse in the SQL `IN` and no duplicate key can occur.
- No `Cache-Control` / `ETag` on the anonymous GET (blind-hunter) — `low`. No endpoint in the app sets cache headers; a speculative perf enhancement with no demonstrated problem (single indexed PK lookup + one in-process call). Fix adds middleware/ETag complexity.
- Test scaffolding (`Password`, `NewEmail()`, `NewClient()`) copy-pasted between the two new suites (blind-hunter) — `low`. Matches the established per-suite-helper convention, including 2.1a's `JobPostingsEndpointsTests`; hoisting is a cross-suite refactor.
- `ProblemBody` record should live in `TestSupport.cs` (blind-hunter) — `low`. Exactly one user; local scope is right until a second suite needs it.
- OpenAPI test does not assert the GET op has no `security` requirement (blind-hunter) — `low`. Anonymous access is already pinned behaviorally: `Get_posting_by_id_without_a_session_returns_200` fails if `.AllowAnonymous()` is dropped or an auth filter is added.
- Batch getters return a mutable `Dictionary` up-cast to `IReadOnlyDictionary` (blind-hunter) — `low`. Each call allocates a fresh dictionary of immutable `record` values; a downcast-and-mutate touches only the caller's throwaway copy — no shared or cross-request state.
- `SeedCompanyAndPostingAsync` over-seeds a posting for the GetCompany-only test and "only incidentally" round-trips the name (blind-hunter) — `low`. The test asserts `contract-retrieved DisplayName == the registered value`, which is a real round-trip; the extra posting is one HTTP call.
- Transient DB / `GetCompany` error → bare 500 (edge-case-hunter E1) — routed to `defer` above (pre-existing, app-wide), not rejected.
- No `cancellationToken.ThrowIfCancellationRequested()` before the synchronous `GetCompany` call (edge-case-hunter E2) — `low`. `IIdentityApi` is synchronous by AD-19 / the Story 2.2 AC; sync methods carry no token. Saves one indexed lookup on a mid-request abort; no handler in the repo does inter-operation cancellation checks.
- Generated client `getById("")` builds `/api/job-postings/` and misroutes (edge-case-hunter E3) — `false` / out of scope. NSwag-generated code (spec: "No hand edits"); the pattern is identical in every generated method; and the server returns 404 → the client still throws `ApiException`, so the caller gets an error, not a silent wrong result.

## Design Notes

- **The name is resolved in the handler, not the Contract.** `GetJobPostingByIdHandler` injects `IIdentityApi` and calls the singular `GetCompany` for the one posting's owner — a single in-process call, no N+1 (this is a detail read, not a list). `IJobPostingsApi`'s own implementation stays free of `IIdentityApi` — `JobPostingSummaryDto` carries `OwnerCompanyId`, and a consumer that needs names resolves them via `IIdentityApi.GetCompanies`.
- **Unresolvable owner Company → `500`, not a masked `404`.** A posting always carries an `owner_company_id` taken from an authenticated Company's claim at create time, and nothing in v1 deletes a Company, so `GetCompany` returning null for a real posting's owner is a data-integrity violation. The handler returns an RFC 9457 `Results.Problem(statusCode: 500, title: "The job posting could not be loaded.")` — a real `application/problem+json` body, no `Program.cs` exception-handler change needed — rather than inventing a placeholder name or returning `404` (2.1a precedent: a genuine failure surfaces as a 500). Documented with `.ProducesProblem(500)` and covered by an orphan-posting integration test.
- **Synchronous Contract methods.** AD-19 and the Story 2.2 AC specify a synchronous in-process service. The implementations use EF Core's synchronous `SingleOrDefault` / `ToDictionary` over projection queries (`CompanyAccount` / `JobPosting` are tiny rows); the async endpoint handler awaits its own `db` query, then makes the sync Contract call.
- **`{id:guid}` route constraint.** A non-GUID segment never reaches the handler — routing returns `404` — which satisfies "a mistyped id must still return 404". A well-formed but unknown id reaches the handler and gets an RFC 9457 `404` with the title "This posting is no longer available."
- **No Host change.** `AddIdentityModule` (which registers `IIdentityApi`) runs before `AddJobPostingsModule` in `Program.cs`, and the container is built once, so JobPostings' handler resolves `IIdentityApi` with no ordering hazard. `MapJobPostingsModule` already owns `/api/job-postings`; the GET is one more `MapGet` on the same group.

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings, 0 errors.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass, including `JobPostingDetailEndpointTests`, `ContractApiTests`, the extended `OpenApiDocumentTests`, and the unchanged 2.1a / Identity / Architecture / Host suites (Docker required for Testcontainers `postgres:18`).
- `dotnet run --project backend/NexusJob.Host` then `curl -s localhost:2052/openapi/v1.json | jq '.paths["/api/job-postings/{id}"]'` -- expected: a `get` op `JobPostings_GetById` with the `200 {id,title,description,companyName}` schema and a `404` problem response.
- `docker compose up`, then `GET /api/auth/csrf` → `POST /api/auth/register {accountType:"company", name:"Acme Inc.", …}` → `POST /api/job-postings {title,description}` with the token → note the `id`; then `GET /api/job-postings/{id}` with **no cookie** → `200 { id, title, description, companyName:"Acme Inc." }`; `GET /api/job-postings/{new-random-guid}` → `404` problem+json; `GET /api/job-postings/xyz` → `404`.
- `cd frontend && npm ci && npm run generate:api && git status --porcelain src/shared/api` -- expected: the client gains `getById` on the first run; empty on a re-run.
- `npm run lint && npm test -- --run && npm run build` -- expected: all green with the regenerated client.

**Manual checks:**
- `docker compose logs app` after a detail read shows one request line for `/api/job-postings/{id}` with method / status / duration only — no cookie, no title/description text.
- `identity` and `job_postings` schemas are untouched — no migration ran; `__EFMigrationsHistory` in both is unchanged.
