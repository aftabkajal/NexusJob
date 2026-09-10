---
title: 'Applications module and apply endpoints'
type: 'feature'
created: '2026-09-10'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f514b4f52425872e8e205dcac21f7582a14290e7'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An authenticated Job Seeker can view a posting but cannot apply to it — there is no Applications persistence and no apply endpoint, so the core "apply → company sees applicants" loop has no backend, and the Story 3.1b apply surface and Story 3.2 apply-gate have nothing to call.

**Approach:** Stand up the third bounded-context module, Applications, with its own `applications` schema, `DbContext`, and migration history; add `POST /api/applications` (Job Seeker only, CSRF-guarded, idempotent on the `(job_posting_id, job_seeker_id)` unique constraint) and `GET /api/applications/mine?jobPostingId=` (Job Seeker only, reports whether the caller has applied). This is story **3.1a**; the apply surface + AR-10 mirror-slice checkpoint is **3.1b** (`deferred-work.md`); the signed-out apply-gate is Story 3.2.

## Boundaries & Constraints

**Always:**
- AD-4/AD-10: Applications owns `/api/applications*`; the Host is the only caller of `AddApplicationsModule` / `MapApplicationsModule`. AD-14: endpoint delegate → DI-resolved slice handler, no mediator.
- AD-5/AD-6/AD-7: one `ApplicationsDbContext` with `HasDefaultSchema("applications")`, `SearchPath=applications` forced onto the connection string, its own `applications.__EFMigrationsHistory`; `job_posting_id` / `job_seeker_id` are bare `Guid` columns — no FK crosses a schema. The `application` table carries `id` (Guid v7 PK), `job_posting_id`, `job_seeker_id`, `submitted_at` (`timestamptz`) and a `UNIQUE (job_posting_id, job_seeker_id)` constraint defined here and in no other module.
- AD-8: one HTTP request → one `SaveChanges` against the `applications` schema only. AD-13: caller identity always comes from the `NameIdentifier` claim; the account-type gate is the `account_type` claim string `job_seeker`.
- AD-9/idempotency: `POST` attempts the insert and catches the Postgres unique-violation (`PostgresErrorCodes.UniqueViolation`, SQLSTATE 23505), then re-reads and returns `200` with the existing application — never `409`, never `500`. The constraint is the guard; no pre-check `SELECT` gates the insert.
- AD-15: RFC 9457 ProblemDetails on every non-2xx; success returns the resource directly (no envelope, no `Location`). Operation ids `Applications_Create` and `Applications_GetMine` (NSwag emits one `ApplicationsClient` with `create` / `getMine`).
- `POST` filter order mirrors `POST /api/job-postings`: `RequireAuthorization()` → 401, `JobSeekerOnlyEndpointFilter` → 403, `AntiforgeryEndpointFilter` → 400, `DataAnnotationsValidationFilter<CreateApplicationRequest>` → 400. `GET /mine` is `RequireAuthorization()` + `JobSeekerOnlyEndpointFilter` only (no antiforgery on a GET).
- `TreatWarningsAsErrors` stays on; `dotnet build` and the full `dotnet test` (ArchitectureTests, Host.Tests, IntegrationTests) are green, including every unchanged Identity / JobPostings / Architecture row.
- `cd frontend && npm run generate:api` is run after a Host build and the regenerated `frontend/src/shared/api/` committed so the CI drift gate stays green; a second run leaves `git status` clean.

**Never:**
- No `IApplicationsApi` and no change to `NexusJob.Modules.Applications.Contracts` — Applications publishes no cross-module contract this epic (a Company view is served by an Applications HTTP endpoint in 3.4, not a contract edge). Applications *consumes* `IJobPostingsApi.GetPostingOwner` only; its `.csproj` gains a `ProjectReference` to `NexusJob.Modules.JobPostings.Contracts` and nothing else.
- No frontend feature code — the only frontend change is the regenerated `shared/api/`. (3.1b owns the apply-button; 3.2 owns the modal.)
- No `Application` entity fields beyond the four above; no application status/withdraw/ranking; no combined "register and apply" endpoint; no extra indexes beyond the unique constraint (Story 3.3's `job_seeker_id` list query and 3.4's `job_posting_id` list add their own).
- `GET /mine` does not check the posting exists — an unknown `jobPostingId` for which the caller has no row simply yields `{ applied: false }`.
- No shared-kernel extraction; `GetPostingOwner`'s returned owner id is used only for the null/not-null existence check — applying carries no ownership restriction.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| First apply | Job Seeker session + CSRF, `POST /api/applications {jobPostingId}` for an existing posting not yet applied to | `200 ApplicationResponse { id, jobPostingId, submittedAt }`; exactly one row inserted with `job_seeker_id` from `NameIdentifier`, `submitted_at` ≈ now | N/A |
| Repeat apply | same seeker `POST`s again for the same posting | `200` with the **existing** application (same `id`, same `submittedAt`); no second row | insert hits 23505 → caught → re-read → 200 |
| Concurrent duplicates | `Task.WhenAll` of N applies, same seeker + posting | every response `200` with the same `id`; exactly one row | all-but-one insert hits 23505 → caught → 200 |
| Unknown posting | `POST {jobPostingId}` where `IJobPostingsApi.GetPostingOwner` returns `null` | `404` ProblemDetails; no row | `Results.Problem(404)` |
| Missing / unparseable `jobPostingId` | body `{}`, `{"jobPostingId":""}`, or a non-GUID string | `400` validation ProblemDetails naming `jobPostingId`; no row | `DataAnnotationsValidationFilter` / handler `Guid.TryParse` → `Results.ValidationProblem` |
| Empty body | `POST` with no JSON body | `400` validation ProblemDetails | validation filter |
| Anonymous | `POST` with no auth cookie | `401` ProblemDetails; no row | `RequireAuthorization()` |
| Company session | `POST` with a `company` `account_type` claim | `403` ProblemDetails; no row | `JobSeekerOnlyEndpointFilter` |
| Missing / bad CSRF | Job Seeker session, absent or garbage `X-CSRF-TOKEN` | `400` ProblemDetails; no row | `AntiforgeryEndpointFilter` |
| Mine — applied | `GET /api/applications/mine?jobPostingId=X`, caller has a row for X | `200 { applied: true, appliedAt: <submitted_at> }` | N/A |
| Mine — not applied | `GET /mine?jobPostingId=X`, caller has no row for X (X may or may not exist) | `200 { applied: false, appliedAt: null }` | N/A |
| Mine — missing / unparseable `jobPostingId` | `GET /mine` with no or non-GUID `jobPostingId` | `400` validation ProblemDetails | handler `Guid.TryParse` → `Results.ValidationProblem` |
| Mine — anonymous / company | `GET /mine` with no cookie / a `company` session | `401` / `403` ProblemDetails | `RequireAuthorization()` / `JobSeekerOnlyEndpointFilter` |
| OpenAPI document | `GET /openapi/v1.json` | `paths["/api/applications"].post` = `Applications_Create` (body `CreateApplicationRequest`, `200 ApplicationResponse`, 400/401/403); `paths["/api/applications/mine"].get` = `Applications_GetMine` (required `jobPostingId` query param, `200 MyApplicationResponse`) | N/A |

</frozen-after-approval>

## Code Map

**Applications module — persistence** (`backend/NexusJob.Modules.Applications/Persistence/`, all NEW, mirror `JobPostings/Persistence/**`)
- `Application.cs` -- `internal sealed class Application { public Guid Id; public Guid JobPostingId; public Guid JobSeekerId; public DateTimeOffset SubmittedAt; }`.
- `ApplicationsDbContext.cs` -- `public sealed class ApplicationsDbContext(DbContextOptions<ApplicationsDbContext> options) : DbContext(options)`; `internal DbSet<Application> Applications => Set<Application>();`. `OnModelCreating`: `HasDefaultSchema("applications")`; `entity.ToTable("application")`; `HasColumnName` snake_case for all four; `entity.HasIndex(e => new { e.JobPostingId, e.JobSeekerId }).IsUnique();`.
- `ApplicationsDbContextFactory.cs` -- `internal sealed class ApplicationsDbContextFactory : IDesignTimeDbContextFactory<ApplicationsDbContext>`; placeholder connstring with `SearchPath=applications`; `MigrationsHistoryTable("__EFMigrationsHistory", "applications")`.
- `Migrations/*` -- NEW, generated: `dotnet ef migrations add InitialApplications --project backend/NexusJob.Modules.Applications --startup-project backend/NexusJob.Modules.Applications`. `Up` = `EnsureSchema("applications")` + `CreateTable("application", schema:"applications", …)` + unique index on `(job_posting_id, job_seeker_id)`.

**Applications module — auth filters** (`backend/NexusJob.Modules.Applications/Auth/`, all NEW, `internal`)
- `AntiforgeryEndpointFilter.cs` -- verbatim copy of `JobPostings/Auth/AntiforgeryEndpointFilter.cs`.
- `DataAnnotationsValidationFilter.cs` -- verbatim copy of `JobPostings/Auth/DataAnnotationsValidationFilter.cs`.
- `JobSeekerOnlyEndpointFilter.cs` -- copy of `CompanyOnlyEndpointFilter.cs` with `const string JobSeekerAccountType = "job_seeker"` and a "available to Job Seeker accounts only" 403 title.

**Applications module — slices** (`backend/NexusJob.Modules.Applications/Features/`, all NEW)
- `CreateApplication/CreateApplicationRequest.cs` -- `public sealed class` with `string JobPostingId { get; init; } = ""` + `[Required(AllowEmptyStrings = false)]` `[StringLength(36)]`.
- `CreateApplication/ApplicationResponse.cs` -- `public sealed record ApplicationResponse(string Id, string JobPostingId, DateTimeOffset SubmittedAt);`
- `CreateApplication/CreateApplicationEndpoint.cs` -- `internal static class`; `Handle([FromBody] CreateApplicationRequest, [FromServices] CreateApplicationHandler, HttpContext, CancellationToken)` → forwards.
- `CreateApplication/CreateApplicationHandler.cs` -- `internal sealed class CreateApplicationHandler(ApplicationsDbContext db, IJobPostingsApi jobPostingsApi)`. Parse `NameIdentifier` → 401 defensive; `Guid.TryParse(request.JobPostingId)` → `Results.ValidationProblem({["jobPostingId"]=["A valid job posting id is required."]})`; `jobPostingsApi.GetPostingOwner(postingId) is null` → `Results.Problem(404, "This posting is no longer available.")`; build `Application { Id = Guid.CreateVersion7(), JobPostingId, JobSeekerId, SubmittedAt = DateTimeOffset.UtcNow }`, `db.Applications.Add`, one `SaveChangesAsync`; `catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })` → re-query the existing row `(JobPostingId, JobSeekerId)` and return `200` with its `ApplicationResponse`; success → `Results.Ok(new ApplicationResponse(...))`.
- `GetMyApplication/MyApplicationResponse.cs` -- `public sealed record MyApplicationResponse(bool Applied, DateTimeOffset? AppliedAt);`
- `GetMyApplication/GetMyApplicationEndpoint.cs` -- `internal static class`; `Handle([FromQuery] string? jobPostingId, [FromServices] GetMyApplicationHandler, HttpContext, CancellationToken)` → forwards.
- `GetMyApplication/GetMyApplicationHandler.cs` -- `internal sealed class GetMyApplicationHandler(ApplicationsDbContext db)`. Parse `NameIdentifier`; `Guid.TryParse(jobPostingId)` → `Results.ValidationProblem`; `row = await db.Applications.Where(a => a.JobPostingId == postingId && a.JobSeekerId == seekerId).Select(a => (DateTimeOffset?)a.SubmittedAt).SingleOrDefaultAsync(ct)`; `Results.Ok(new MyApplicationResponse(row is not null, row))`.

**Applications module — wiring**
- `backend/NexusJob.Modules.Applications/NexusJob.Modules.Applications.csproj` -- add the EF `PackageReference` block from `JobPostings.csproj` (`Npgsql`, `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` w/ `PrivateAssets=all`); add `<ProjectReference Include="..\NexusJob.Modules.JobPostings.Contracts\NexusJob.Modules.JobPostings.Contracts.csproj" />`.
- `backend/NexusJob.Modules.Applications/ApplicationsModule.cs` -- replace the two no-op stubs. `AddApplicationsModule`: `BuildApplicationsConnectionString` (copy of `BuildJobPostingsConnectionString`, `SearchPath = "applications"`); `AddDbContext<ApplicationsDbContext>` with `MigrationsHistoryTable("__EFMigrationsHistory", "applications")`; `AddScoped` the two handlers + `AntiforgeryEndpointFilter` + `JobSeekerOnlyEndpointFilter`. `MapApplicationsModule`: `var group = endpoints.MapGroup("/api/applications");` then `group.MapPost("", CreateApplicationEndpoint.Handle).WithName("Applications_Create").RequireAuthorization().AddEndpointFilter<JobSeekerOnlyEndpointFilter>().AddEndpointFilter<AntiforgeryEndpointFilter>().AddEndpointFilter(new DataAnnotationsValidationFilter<CreateApplicationRequest>()).Accepts<CreateApplicationRequest>("application/json").Produces<ApplicationResponse>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);` and `group.MapGet("/mine", GetMyApplicationEndpoint.Handle).WithName("Applications_GetMine").RequireAuthorization().AddEndpointFilter<JobSeekerOnlyEndpointFilter>().Produces<MyApplicationResponse>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);`
- `backend/NexusJob.Host/Program.cs` -- in the startup-migration `try` block, add `migrationScope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Database.Migrate();` after the `JobPostingsDbContext` line; add the `using NexusJob.Modules.Applications.Persistence;` import. (`AddApplicationsModule` / `MapApplicationsModule` are already called.)

**Tests** (`backend/NexusJob.IntegrationTests/`)
- `TestSupport.cs` -- add `ApplicationsDatabase(connString)` (`CountApplicationsForPostingAsync(Guid postingId)`, `CountApplicationsForSeekerAsync(Guid seekerId)`, `GetApplicationRowAsync(Guid postingId, Guid seekerId)` → `(Guid id, DateTimeOffset submittedAt)?`, and an `information_schema` probe for the unique constraint); add `AuthApiClient.ApplyAsync(string jobPostingId, string? csrfToken)` + a CSRF-seeding overload, and `GetMyApplicationAsync(string jobPostingId)` (GET, no CSRF); add `ApplicationDto(id, jobPostingId, submittedAt)` and `MyApplicationDto(applied, appliedAt)` records.
- `ApplicationsEndpointsTests.cs` -- NEW. `[Collection(nameof(IdentityApiCollection))]`. One `[Fact]` per I/O-matrix row: first apply (row count + Guid v7 + `NameIdentifier` == `job_seeker_id`), repeat apply (same id/timestamp, count unchanged), `Task.WhenAll` concurrent duplicates (one row, all responses same id), unknown posting → 404, missing/blank/non-GUID `jobPostingId` → 400, empty body → 400, anonymous → 401, company session → 403, missing/garbage CSRF → 400, `mine` applied / not-applied / bogus-posting-not-applied / missing-param-400 / anonymous-401 / company-403, and a migration-shape test (`applications.__EFMigrationsHistory` has `InitialApplications`; `applications.application` columns + unique constraint on `(job_posting_id, job_seeker_id)`).
- `OpenApiDocumentTests.cs` -- extend: assert both new operations, their operation ids, the required `jobPostingId` query param on `GET /api/applications/mine`, and the `200` response schemas.

**Reference — do not change**
- `backend/NexusJob.Modules.JobPostings/Features/CreateJobPosting/**` -- the write-slice shape (endpoint/handler/request/response, `Guid.CreateVersion7()`, single `SaveChanges`, `200` + bare resource) being mirrored.
- `backend/NexusJob.Modules.Identity/Features/Register/RegisterHandler.cs` -- the `catch (DbUpdateException … PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })` idempotency pattern.
- `backend/NexusJob.Modules.JobPostings/Auth/{CompanyOnlyEndpointFilter,AntiforgeryEndpointFilter,DataAnnotationsValidationFilter}.cs` -- the filters being copied.
- `backend/NexusJob.Modules.JobPostings.Contracts/IJobPostingsApi.cs` -- `Guid? GetPostingOwner(Guid)` is the existence check; already published, do not modify.
- `backend/NexusJob.ArchitectureTests/**` -- `NexusJob.Modules.Applications` is already registered in every lookup (`ImplementationNames`, `RawSqlSchemaScan.ModuleSchemas` → `applications`, `KnownSchemas`, `.sln`, `BoundaryScanProject`); no arch-test edits — the suite must stay green.
- `_bmad-output/implementation-artifacts/epic-3-context.md` -- epic constraints (idempotent apply, 404-never-403, no `IApplicationsApi`).

## Tasks & Acceptance

**Execution:**
- [x] `backend/NexusJob.Modules.Applications/NexusJob.Modules.Applications.csproj` -- add EF package block + `JobPostings.Contracts` project reference.
- [x] `backend/NexusJob.Modules.Applications/Persistence/{Application,ApplicationsDbContext,ApplicationsDbContextFactory}.cs` -- the `applications`-schema entity, context (unique index on `(job_posting_id, job_seeker_id)`), and design-time factory.
- [x] `backend/NexusJob.Modules.Applications/Persistence/Migrations/*` -- `dotnet ef migrations add InitialApplications` for `ApplicationsDbContext`; verify `Up` creates the schema, table, and unique constraint.
- [x] `backend/NexusJob.Modules.Applications/Auth/{AntiforgeryEndpointFilter,DataAnnotationsValidationFilter,JobSeekerOnlyEndpointFilter}.cs` -- the three `internal` filters (first two verbatim copies; the third a `job_seeker` variant of `CompanyOnlyEndpointFilter`).
- [x] `backend/NexusJob.Modules.Applications/Features/CreateApplication/{CreateApplicationRequest,ApplicationResponse,CreateApplicationEndpoint,CreateApplicationHandler}.cs` -- the apply slice (existence check → insert → 23505 catch → 200 existing).
- [x] `backend/NexusJob.Modules.Applications/Features/GetMyApplication/{MyApplicationResponse,GetMyApplicationEndpoint,GetMyApplicationHandler}.cs` -- the `?jobPostingId=` probe.
- [x] `backend/NexusJob.Modules.Applications/ApplicationsModule.cs` -- real `AddApplicationsModule` (DbContext + DI) and `MapApplicationsModule` (`/api/applications` group, both endpoints, filter chain, `WithName`).
- [x] `backend/NexusJob.Host/Program.cs` -- add `ApplicationsDbContext.Database.Migrate()` to the startup-migration scope + the import.
- [x] `backend/NexusJob.IntegrationTests/TestSupport.cs` -- `ApplicationsDatabase` helper, `AuthApiClient.ApplyAsync` / `GetMyApplicationAsync`, the two DTOs.
- [x] `backend/NexusJob.IntegrationTests/ApplicationsEndpointsTests.cs` -- one test per I/O-matrix row incl. the concurrent-duplicate and migration-shape tests.
- [x] `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` -- assert both `Applications_*` operations and their schemas.
- [x] `frontend/src/shared/api/*` -- `npm run generate:api` after a Host build; commit the regenerated `ApplicationsClient`; verify drift-clean on re-run.

**Acceptance Criteria:**
- Given a signed-in Job Seeker who has not applied, when they `POST /api/applications { jobPostingId }` for an existing posting, then the response is `200 { id, jobPostingId, submittedAt }`, exactly one `applications.application` row exists with `job_seeker_id` equal to the caller's `NameIdentifier`, and `id` is a Guid v7.
- Given that Job Seeker has already applied, when the same `POST` is repeated (including several requests fired concurrently), then every response is `200` carrying the original application's `id` and `submittedAt`, and the row count for that `(posting, seeker)` pair stays exactly 1 — no `409`, no `500`.
- Given an unauthenticated caller, a `company` session, or a missing/invalid `X-CSRF-TOKEN`, when `POST /api/applications` is called, then the response is `401` / `403` / `400` ProblemDetails respectively and no row is written.
- Given a signed-in Job Seeker, when `GET /api/applications/mine?jobPostingId={id}` is called, then it returns `200 { applied: true, appliedAt }` when they have a row for that posting and `200 { applied: false, appliedAt: null }` otherwise, and this endpoint lives entirely in the Applications module.
- Given `dotnet build backend/NexusJob.sln` then `dotnet test backend/NexusJob.sln`, when they run, then the build is warning-free and every suite passes — the boundary / raw-SQL / registry gates, the unchanged Identity & JobPostings suites, and the new Applications + OpenAPI tests.
- Given a Host build then `cd frontend && npm run generate:api`, when it runs, then `frontend/src/shared/api/` gains an `ApplicationsClient` with `create` / `getMine`, a second run leaves `git status` clean, and `npm run lint` / `npm test` / `npm run build` pass.

## Implementation Notes

## Spec Change Log

## Review Triage Log

Three layers ran on the diff since `baseline_commit`: blind-hunter (13 findings), edge-case-hunter (7 findings), verification-gap (0 findings — every I/O-matrix row maps to a running test with real assertions; migration/schema and Host wiring covered).

| # | Source | Location | Finding | Verdict | Evidence |
|---|---|---|---|---|---|
| 1 | blind-hunter, edge-case-hunter | `ApplicationsModule.cs` `MapPost` chain | `POST /api/applications` declares only `.ProducesProblem(400/401/403)` — no `404`, so the regenerated `ApplicationsClient.processCreate` has no `status === 404` branch and maps the "posting no longer available" 404 to `throwException("An unexpected server error occurred.")` | medium | Verified: `nexus-api-client.ts:496-525` `processCreate` branches `200/400/401/403` then `else` → generic error; the sibling `JobPostingsClient.processGetById` (`:436-456`) *does* have a `404` branch because `GET /api/job-postings/{id}` declares `.ProducesProblem(404)`. The 404 is a real, tested runtime path (`Apply_to_an_unknown_posting_returns_404...`). |
| 2 | blind-hunter, edge-case-hunter | `CreateApplicationHandler.cs` / `GetMyApplicationHandler.cs` vs `DataAnnotationsValidationFilter.cs` | Validation-error key casing splits by which guard trips: the handlers' `Guid.TryParse` failure hardcodes `errors["jobPostingId"]` (camelCase); the `DataAnnotationsValidationFilter` path emits `errors["JobPostingId"]` (CLR member name). Same field, two wire casings. | medium | Verified: handlers write `["jobPostingId"]` literally; the shared filter's `result.MemberNames` yields `"JobPostingId"`; the Host's `ConfigureHttpJsonOptions` sets no `DictionaryKeyPolicy`, so dictionary keys serialize verbatim. Codebase convention (JobPostings/Identity via the same filter) is the CLR PascalCase name; the hand-rolled camelCase key in the handlers is the deviation. Tests only survive via case-insensitive `Contains`. |
| 3 | blind-hunter | `ApplicationsEndpointsTests.cs` | No test discriminates the unique index / `/mine` query on **both** columns: missing (a) two different Job Seekers apply to the same posting → two rows; (b) a seeker who applied to posting A gets `applied:false` from `GET /mine?jobPostingId=B`. A regression narrowing the index to `UNIQUE(job_posting_id)` or the `/mine` `Where` to `job_seeker_id` alone would pass all 23 current tests. | medium | Verified by enumerating the test file: every apply/mine test uses one `(posting, seeker)` pair; `Mine_returns_applied_false_for_an_unknown_posting_id...` uses a seeker who applied to nothing, so a `job_seeker_id`-only query would also return false. The epic invariant ("at most one application per (Job Seeker, Job Posting) pair") is not pinned on the second column. |
| 4 | blind-hunter, edge-case-hunter | `CreateApplicationRequest.cs` `[StringLength(36)]` | The bound rejects otherwise-parseable non-hyphenated GUID forms (`{…}` / `(…)` = 38 chars, `{0x…}` longer) with a length message instead of the intended `Guid.TryParse` path. | low | Verified: `[StringLength(36)]` admits only the 32-char "N" and 36-char "D" forms. Real, but practically unreachable — the API only ever emits `id.ToString()` ("D", 36 chars) and the generated client passes that string through; no caller produces the brace/paren forms. Fix is a direct correction (widen the bound). |
| 5 | blind-hunter | `ApplicationsDbContext.cs` / migration | Spec says "UNIQUE constraint"; `HasIndex(...).IsUnique()` emits `CREATE UNIQUE INDEX`, which does not appear in `information_schema.table_constraints`. | false | Verified: a Postgres unique index enforces uniqueness and raises SQLSTATE 23505 identically to a UNIQUE constraint — idempotency works and all 106 tests pass. The migration-shape test correctly probes `pg_index` (not `table_constraints`) for the pair index and passes. No functional or verification defect; the only artifact is the word "constraint" in spec prose, and fixing that would edit this build's spec. |
| 6 | blind-hunter | `ApplicationsEndpointsTests.cs` `AssertProblemDetailsAsync` | The distinctive `403`/`400` filter titles/details are not asserted (only content-type + `status`). | low | The code copy is correct and static; no defect occurs today. The I/O matrix requires "ProblemDetails", not specific title text, and the JobPostings sibling tests assert at the same depth. A "could be more thorough" observation, not an occurring bad outcome. |
| 7 | blind-hunter | `nexus-api-client.ts` `MyApplicationResponse.appliedAt` | Generated type is `string \| undefined`, but the server always sends `"appliedAt": null` (`JsonIgnoreCondition.Never`). | low | Generic NSwag behavior for any nullable date in this codebase's client; there is no in-repo consumer (3.1b/3.2 deferred); the spec Design Notes already mandate the safe rule ("3.1b branches on `applied`, not on the key's presence"). |
| 8 | blind-hunter | `CreateApplicationHandler.cs` | No `ILogger` on the 23505-caught path or the "impossible" 500 fall-through — no signal for concurrent/duplicate-apply frequency in the pilot module. | low | Matches every sibling handler (`RegisterHandler`, `CreateJobPostingHandler` inject no logger); Host `AddHttpLogging` emits one line per request (method/path/status/duration); no spec requirement. The AR-10 pilot is about the frontend slice mapping (3.1b), not backend observability. |
| 9 | blind-hunter | `OpenApiDocumentTests.cs` | New tests are asymmetric (POST asserts 4xx media type; GET `/mine` asserts only status codes present) and assert no schema metadata (`maxLength`, required request property). | low | Core assertions (operation ids, response schemas, required `jobPostingId` param, POST problem+json) are present. Test-depth only; the one actionable part — POST must declare `404` — is folded into finding #1's patch. |
| 10 | blind-hunter | `ApplicationsModule.cs` `.Accepts<T>("application/json")` | `415 Unsupported Media Type` for a non-JSON POST is neither in the I/O matrix nor `.ProducesProblem`. | low | Pre-existing, codebase-wide: `POST /api/job-postings` has the identical `.Accepts` with no `.ProducesProblem(415)` and no 415 test. The generated client always sends `application/json`, so it is unreachable in practice. |
| 11 | blind-hunter | `epic-3-context.md` | The compiled epic context still lists a single "Story 3.1" and attributes the apply surface + AR-10 checkpoint to it, and phrases `/mine` as `{ applied, appliedAt }` or `{ applied: false }` (two shapes) — contradicting the 3.1a/3.1b split and the always-present `appliedAt`. | low | Verified: `epic-3-context.md` Stories list + Cross-Story Dependencies + Technical Decisions. It is a faithful distillation of planning docs (`epics.md` still has one un-split Story 3.1); the split is a build-time decision tracked in the spec, `deferred-work.md`, and `sprint-status.yaml`. The precise 3-1a spec is what implementers follow, and it is correct. Fix edits an agent-context file. |
| 12 | blind-hunter | `sprint-status.yaml` | The 3.1a/3.1b split is recorded only as a prose `# split:` comment on an `in-progress` line — not machine-readable. | low | Matches the established convention for every prior split (2.1/2.2/2.3 use the same `# split:` comment form). Not this story's code; changing it is a project-wide tracking-schema decision. |
| 13 | edge-case-hunter | `CreateApplicationHandler.cs` success return | First-apply `200` returns in-memory `DateTimeOffset.UtcNow` (100 ns ticks); Postgres `timestamptz` truncates to µs, so the first response's `submittedAt` can differ sub-µs from a later `GET /mine` `appliedAt` or an idempotent replay. | low | Verified: identical to `CreateJobPostingHandler` (`CreatedAt = DateTimeOffset.UtcNow`, returned without re-read). `First_apply`/`Repeat_apply` tests compare with `TimeSpan.FromMilliseconds(1)` tolerance; no consumer does exact timestamp equality (3.1b branches on `applied`). A sub-µs difference is not a semantic AC violation; the fix would add a round-trip or rounding to the happy path. |
| 14 | edge-case-hunter | `CreateApplicationHandler.cs` catch → `SingleAsync` | If the 23505 is caught but the follow-up re-read finds no row (manual delete now, or a future withdraw feature), `SingleAsync` throws → uncontrolled 500. | false | Verified: no code path deletes an `application` row in this diff or anywhere in v1 (no withdraw, no FK cascade — bare `Guid` columns). The scenario is unreachable, and the spec Design Notes deliberately choose the loud 500 over an invented response. Loudly failing on a genuinely-unreachable state is correct behavior; a future withdraw story owns re-checking this. |

**Routing** (survivors grouped by shared root cause; `false` findings above are rejected outright):
- **patch** — #1 (declare `404` on `POST /api/applications` + regen client + extend the OpenAPI test), #2 (normalize both handlers' validation-error key to the CLR name `JobPostingId`), #3 (add the two discriminating tests), #4 (widen `[StringLength(36)]` so `Guid.TryParse` stays the real check). Each fix is trivial, self-contained, adds no public surface.
- **defer** — #11 (`epic-3-context.md` predates/omits the 3.1a/3.1b split and the precise `/mine` shape; fix edits an agent-context file). Appended to `deferred-work.md`.
- **rejected (low / false, no action)** — #5, #6, #7, #8, #9, #10, #12, #13, #14 — each logged above with its refutation or negligible-harm rationale.

## Design Notes

- **`jobPostingId` is a `string` on the request, parsed in the handler.** Consistent with `CreateJobPostingRequest` (all `string` props, trimmed/validated in code) and it yields a deterministic `400` validation ProblemDetails naming `jobPostingId` for a malformed GUID, rather than a framework `BadHttpRequestException` from JSON binding a `Guid`. `[StringLength(36)]` bounds it; the handler's `Guid.TryParse` is the real check.
- **`GET /mine` does not verify the posting exists.** Its only job is "has *this* seeker applied to this id" — a bogus or deleted id the seeker never applied to is indistinguishable from a real one they haven't applied to, and both correctly return `{ applied: false }`. Adding a `GetPostingOwner` call would be dead weight and would leak posting existence on an endpoint that has no reason to.
- **`MyApplicationResponse.AppliedAt` serializes as `"appliedAt": null` when not applied.** The Host sets `JsonIgnoreCondition.Never` app-wide, so the nullable is always present on the wire. The AC's `{ applied: false }` is shorthand; 3.1b branches on `applied`, not on the key's presence.
- **Idempotency: constraint-first, re-read for the body.** The insert is attempted with no guarding `SELECT` (per AD-9 / epic context); the only `SELECT` runs *after* a caught 23505 purely to build the `200` body from the existing row. Mirrors `RegisterHandler`'s duplicate-email path. A caught 23505 whose follow-up read finds no row is impossible in v1 (nothing deletes an application) — let that fall through to a `500` rather than inventing a response.
- **Story 3.3 will overload `GET /api/applications/mine`.** 3.1a's form takes a required `jobPostingId`; 3.3 adds a no-`jobPostingId` `?page=&pageSize=` paged-list form on the same route. Keep the 3.1a handler's parameter handling narrow so 3.3 can branch cleanly.

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings, 0 errors.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass, including `ApplicationsEndpointsTests`, the extended `OpenApiDocumentTests`, and every unchanged Identity / JobPostings / Architecture / Host suite (Docker required for Testcontainers `postgres:18`).
- `dotnet run --project backend/NexusJob.Host` then, as a registered Job Seeker (cookie + `X-CSRF-TOKEN`), `curl -sib … -X POST localhost:2052/api/applications -d '{"jobPostingId":"<id>"}'` twice -- expected: `200` both times, identical `id`, one DB row.
- `cd frontend && npm ci && npm run generate:api && git status --porcelain src/shared/api` -- expected: gains `ApplicationsClient` on the first run; empty on a re-run.
- `npm run lint && npm test -- --run && npm run build` -- expected: all green with the regenerated client.

**Manual checks:**
- `docker compose up`, register a Job Seeker and a Company; the Company publishes a posting. The Job Seeker `POST`s `/api/applications` for it → `200`; `GET /api/applications/mine?jobPostingId=<id>` → `{ applied: true, appliedAt }`. The Company `POST`ing the same → `403`. An anonymous `POST` → `401`.
- `psql`: `applications.application` has exactly the four columns and a unique constraint on `(job_posting_id, job_seeker_id)`; `identity` and `job_postings` schemas and their `__EFMigrationsHistory` are unchanged.
