---
title: 'JobPostings module and the POST /api/job-postings create endpoint'
type: 'feature'
created: '2026-09-07'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '0bed7001810229b06374b9c98bfea611cf58b2e3'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `NexusJob.Modules.JobPostings` is a wiring-only stub — no persistence, no schema, no endpoint. A Company cannot create a job posting, so nothing in Epic 2 (search, detail) or Epic 3 (apply, applicants) can proceed.

**Approach:** Stand up the JobPostings module: its own `job_postings`-schema persistence (`JobPosting` entity, EF Core `DbContext`, its own migration history), and the create half of its API — `POST /api/job-postings` (authenticated Company only, antiforgery-protected). Add the Host startup-migration hook for the new `DbContext`, regenerate the committed OpenAPI TypeScript client, and cover every behaviour with Testcontainers integration tests. This is story **2.1a**; the Post-a-Job frontend surface is **2.1b** (see `deferred-work.md`).

## Boundaries & Constraints

**Always:**
- Follow the architecture ADs as Identity 1.3a did: AD-1 (JobPostings may reference only other modules' `.Contracts`; `.Contracts` references no implementation — the ArchUnitNET gates enforce this and must stay green), AD-4 (`JobPosting` = `id`, `owner_company_id`, `title`, `description`, `created_at`; JobPostings is the sole owner), AD-5 (`JobPostingsDbContext` maps only JobPostings entities, `search_path`/default schema = `job_postings`; no cross-schema SQL), AD-6/AD-7 (own EF migration history in `job_postings.__EFMigrationsHistory`), AD-8 (one write request = one module, one `SaveChanges` against `job_postings` only), AD-10 (Host is the sole composition root; JobPostings exposes only `AddJobPostingsModule` / `MapJobPostingsModule`), AD-13 (caller identity read from the cookie's `NameIdentifier` claim only), AD-14 (endpoint delegate calls a slice handler resolved from DI — no mediator; cross-cutting concerns are endpoint filters), AD-15 (RFC 9457 ProblemDetails on every non-2xx; success returns the resource representation directly, no envelope; one OpenAPI 3.0 document, generated TS client is the only client-side API description), AD-18 (create-posting requires an authenticated session).
- Identifiers: `id` and `owner_company_id` are `Guid`; `id` generated with `Guid.CreateVersion7()` in app code, `owner_company_id` parsed from the `NameIdentifier` claim. `Guid` in C#, string only at the JSON edge. DB columns snake_case: `id`, `owner_company_id`, `title`, `description`, `created_at`. `owner_company_id` is a plain column — **no foreign key to any schema**.
- `created_at` is the current UTC instant, stored `timestamptz`, serialised as ISO-8601 (`createdAt`).
- A state-changing `POST /api/job-postings` requires a valid antiforgery token (the existing double-submit `X-CSRF-TOKEN` scheme, seeded by `GET /api/auth/csrf`); a missing/invalid token is a `400` ProblemDetails and no row is written.
- `TreatWarningsAsErrors` stays on; `dotnet build` and the full `dotnet test` (ArchitectureTests, Host.Tests, IntegrationTests) are green, including the unchanged Identity rows and `HealthEndpointTests` (the Host still starts with the DB unreachable).
- Every row of the I/O matrix is covered by an integration test that runs against a real Postgres (Testcontainers) and passes.
- `cd frontend && npm run generate:api` is run and the regenerated `frontend/src/shared/api/` is committed, so the CI `openapi-client` drift gate stays green. `npm run lint` / `npm test` / `npm run build` stay green (the new generated client is `eslint`-ignored, still `tsc`-type-checked).

**Never:**
- No frontend feature code — no `pages/post-a-job`, no `features/create-posting`, no `entities/job-posting`, no nav change, no hand-written consumer of the new client. The only frontend change is the regenerated `shared/api/` (2.1b owns the surface).
- No `GET` endpoints (detail is 2.2, search is 2.3), no `IJobPostingsApi` / `IIdentityApi` Contract implementation or DTOs (2.2), no `Applications` module change.
- No reference from JobPostings to any Identity implementation type — the Company-only check reads the `account_type` claim **string** (part of the cookie-auth wire contract, AD-13), not Identity's `internal AccountType` type.
- No edit / delete / deactivate endpoint, no posting lifecycle, no ownership-transfer.
- No cross-schema foreign key, no raw cross-schema SQL, no mediator, no FluentValidation (use built-in DataAnnotations via an endpoint filter, mirroring Identity), no domain-event bus.
- No secret, connection string, or config value committed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Create a posting | `POST /api/job-postings` `{ title, description }` (both non-empty), authenticated **Company** session, valid antiforgery token | `200` with `{ id, title, description, createdAt }` (no envelope); exactly one `job_posting` row in the `job_postings` schema, `owner_company_id` = the caller's `NameIdentifier`, `created_at` ≈ now (UTC), single `SaveChanges` | N/A |
| Empty / whitespace title or description | one or both blank / whitespace | `400` validation ProblemDetails naming the invalid field(s); no row | ProblemDetails (validation filter) |
| Unauthenticated caller | no auth cookie | `401` ProblemDetails; no row | ProblemDetails (JSON, never an HTML redirect for `/api/*`) |
| Job Seeker session | valid auth cookie whose `account_type` claim is `job_seeker` | `403` ProblemDetails; no row | ProblemDetails |
| Missing / invalid antiforgery token | authenticated Company, `POST` without a valid `X-CSRF-TOKEN` | `400` ProblemDetails (antiforgery); no row | ProblemDetails |
| Missing / malformed body | `null` / empty JSON body | `400` ProblemDetails; no row | ProblemDetails (`{ "body": ["A request body is required."] }`) |
| Structured logs during any of the above | any request | one JSON log line per request (method/path/status/duration only); no cookie, `X-CSRF-TOKEN`, or body content in the logs | N/A |
| Host starts with the database reachable | schema empty or behind | `JobPostingsDbContext` pending migrations apply before the app serves traffic (alongside the existing `IdentityDbContext` / `DataProtectionKeysDbContext`); `/health` is `200` | N/A |
| Host starts with the database down / misconfigured | connection string unreachable / absent | the Host still starts; the startup-migration failure is logged once (no crash); `/health` reports `503` until a later restart | migration exception caught + logged (existing behavior, extended to the new context) |
| Fetch the OpenAPI document | `GET /openapi/v1.json` on the running Host | `paths` contains `/api/job-postings` with a `post` operation, its `200` `{id,title,description,createdAt}` schema, and `ProducesProblem` `400` / `401` / `403` | N/A |

</frozen-after-approval>

## Resolved Decisions

- **The Company-only guard is `RequireAuthorization()` (→ `401` for anonymous) plus an endpoint filter that returns `403` when the `account_type` claim is not `"company"`.** JobPostings defines its own `const string AccountTypeClaim = "account_type"` / `CompanyAccountType = "company"` — it reads the claim value, not Identity's `internal AccountType` type (AD-1). An authenticated Job Seeker therefore gets `403`, not `401`.
- **JobPostings carries its own thin `internal` copies of `DataAnnotationsValidationFilter<TRequest>` and `AntiforgeryEndpointFilter`**, mirroring Identity's (`.NET 10`'s `AddValidation()` source generator only scans the web-SDK Host project, and `UseAntiforgery()` only auto-validates form endpoints). Extracting a shared kernel for these ~20-line filters is a future refactor, out of scope here.
- **`JobPostingsDbContext` is `public`** (like `IdentityDbContext`) so the Host composition root can resolve it and call `Database.Migrate()` at startup; its `DbSet<JobPosting>` and the entity stay `internal`, so no other project can use them (AD-1 already blocks a project reference).
- **Reuse the existing `IdentityApiCollection` / `IdentityApiFixture`** for the new tests — it already boots the whole Host (all three modules) against one `postgres:18` container and runs the Host startup migration. No new fixture; do not rename the existing one.
- **Response is `200` with the representation** (`{ id, title, description, createdAt }`), not `201 Created` — consistent with Identity's register (`200`, no `Location`), and the frontend (2.1b) renders the confirmation directly.

## Code Map

**Backend — module**
- `backend/NexusJob.Modules.JobPostings/NexusJob.Modules.JobPostings.csproj` -- add the EF Core + Npgsql package refs exactly as `NexusJob.Modules.Identity.csproj` has them (`Npgsql`, `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets=all`). Keep the `FrameworkReference` and the `.Contracts` project ref.
- `backend/NexusJob.Modules.JobPostings/Persistence/JobPosting.cs` -- NEW. `internal sealed class`: `Guid Id`, `Guid OwnerCompanyId`, `string Title`, `string Description`, `DateTimeOffset CreatedAt`.
- `backend/NexusJob.Modules.JobPostings/Persistence/JobPostingsDbContext.cs` -- NEW. `public sealed class JobPostingsDbContext(DbContextOptions<JobPostingsDbContext>) : DbContext`. `internal DbSet<JobPosting> JobPostings`. `OnModelCreating`: `HasDefaultSchema("job_postings")`; `ToTable("job_posting")`; snake_case columns (`id`, `owner_company_id`, `title`, `description`, `created_at`); `HasKey(e => e.Id)`. Mirror `IdentityDbContext`'s shape.
- `backend/NexusJob.Modules.JobPostings/Persistence/JobPostingsDbContextFactory.cs` -- NEW. `internal sealed` `IDesignTimeDbContextFactory<JobPostingsDbContext>` with a parse-only placeholder connection string + `MigrationsHistoryTable("__EFMigrationsHistory", "job_postings")`. Copy `IdentityDbContextFactory.cs`.
- `backend/NexusJob.Modules.JobPostings/Persistence/Migrations/*` -- NEW. `dotnet ef migrations add InitialJobPostings --project backend/NexusJob.Modules.JobPostings`: creates `job_postings.job_posting` (`id` uuid PK, `owner_company_id` uuid not null, `title` text not null, `description` text not null, `created_at` timestamptz not null); history table `job_postings.__EFMigrationsHistory`.
- `backend/NexusJob.Modules.JobPostings/Auth/AntiforgeryEndpointFilter.cs` -- NEW. Copy of `NexusJob.Modules.Identity/Auth/AntiforgeryEndpointFilter.cs` (namespace changed). `internal sealed`.
- `backend/NexusJob.Modules.JobPostings/Auth/DataAnnotationsValidationFilter.cs` -- NEW. Copy of `NexusJob.Modules.Identity/Auth/DataAnnotationsValidationFilter.cs` (namespace changed). `internal sealed`.
- `backend/NexusJob.Modules.JobPostings/Auth/CompanyOnlyEndpointFilter.cs` -- NEW. `internal sealed` `IEndpointFilter`: after `RequireAuthorization()` has admitted the caller, read `context.HttpContext.User.FindFirst("account_type")?.Value`; if it is not `"company"`, return `Results.Problem(title: "This action is available to Company accounts only.", statusCode: 403)` and do not call `next`. Constants live here.
- `backend/NexusJob.Modules.JobPostings/Features/CreateJobPosting/CreateJobPostingRequest.cs` -- NEW. `public sealed class` with `[Required(AllowEmptyStrings = false)] [StringLength(...)]` `Title` and `Description` (pick sane max lengths, e.g. 200 / 4000; whitespace-only fails `[Required]` — `RequiredAttribute` trims when `AllowEmptyStrings = false`).
- `backend/NexusJob.Modules.JobPostings/Features/CreateJobPosting/JobPostingResponse.cs` -- NEW. `public sealed record JobPostingResponse(string Id, string Title, string Description, DateTimeOffset CreatedAt)` (or `string CreatedAt` ISO-8601 — match how Identity serialises timestamps; `AuthAccountResponse` uses plain strings for ids).
- `backend/NexusJob.Modules.JobPostings/Features/CreateJobPosting/CreateJobPostingEndpoint.cs` -- NEW. `internal static` delegate: `[FromBody] CreateJobPostingRequest`, `[FromServices] CreateJobPostingHandler`, `HttpContext`, `CancellationToken` → `handler.HandleAsync(...)`.
- `backend/NexusJob.Modules.JobPostings/Features/CreateJobPosting/CreateJobPostingHandler.cs` -- NEW. `internal sealed class(JobPostingsDbContext db)`. Parse `owner_company_id` from `httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` (→ `401` `Results.Problem` if unparseable, defensive — `RequireAuthorization` already guarantees a principal). Build `new JobPosting { Id = Guid.CreateVersion7(), OwnerCompanyId = ownerId, Title = request.Title.Trim(), Description = request.Description.Trim(), CreatedAt = DateTimeOffset.UtcNow }`, `db.JobPostings.Add(...)`, one `SaveChangesAsync`, return `Results.Ok(new JobPostingResponse(...))`.
- `backend/NexusJob.Modules.JobPostings/JobPostingsModule.cs` -- flesh out `AddJobPostingsModule`: `services.AddDbContext<JobPostingsDbContext>(o => o.UseNpgsql(connString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "job_postings")))` with `SearchPath=job_postings` forced on (mirror `IdentityModule.BuildIdentityConnectionString`, including the unconfigured-placeholder fallback with `Timeout=3;Command Timeout=3`); `services.AddScoped<AntiforgeryEndpointFilter>()`, `<CompanyOnlyEndpointFilter>()`, `<CreateJobPostingHandler>()`. `MapJobPostingsModule`: `var group = endpoints.MapGroup("/api/job-postings"); group.MapPost("", CreateJobPostingEndpoint.Handle).WithName("JobPostings_Create").RequireAuthorization().AddEndpointFilter<CompanyOnlyEndpointFilter>().AddEndpointFilter<AntiforgeryEndpointFilter>().AddEndpointFilter(new DataAnnotationsValidationFilter<CreateJobPostingRequest>()).Accepts<CreateJobPostingRequest>("application/json").Produces<JobPostingResponse>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403);`.

**Backend — Host**
- `backend/NexusJob.Host/Program.cs` -- in the `using (var migrationScope = …)` block, add `migrationScope.ServiceProvider.GetRequiredService<JobPostingsDbContext>().Database.Migrate();` after the two existing `Migrate()` calls (inside the same `try`); add `using NexusJob.Modules.JobPostings.Persistence;`. No other change — `AddJobPostingsModule` / `MapJobPostingsModule` are already called, and the middleware order (`UseAuthentication`/`UseAuthorization`/`UseAntiforgery` before the module maps) already fits.
- `backend/NexusJob.Host/NexusJob.Host.csproj` -- no change (already references the JobPostings impl project and the EF packages).

**Backend — tests**
- `backend/NexusJob.IntegrationTests/AuthApiClient` / `TestSupport.cs` -- add a `CreatePostingAsync(string title, string description, string? csrfToken)` helper on `AuthApiClient` (seed the token like `RegisterAsync` does), and a `JobPostingsDatabase` (or extend the existing `IdentityDatabase` — prefer a new small class) with `CountPostingsForOwnerAsync(Guid ownerId)` / `GetPostingAsync(Guid id)` querying `job_postings.job_posting`.
- `backend/NexusJob.IntegrationTests/JobPostingsEndpointsTests.cs` -- NEW. `[Collection(nameof(IdentityApiCollection))]`. One `[Fact]` per I/O-matrix row: create happy path (`200`, one row, `owner_company_id` = the registered Company's id, `created_at` ≈ now); empty/whitespace title and description → `400` naming the field; unauthenticated → `401`; Job Seeker session (register with `accountType:"job_seeker"`, 1.4a) → `403`; no antiforgery token → `400`; null/empty body → `400`; log-redaction for a create; and an `OpenApiDocumentTests`-style assertion (extend `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs`) that `/api/job-postings` `post` is in the document with its `200` shape and `ProducesProblem` codes.

**Frontend**
- `frontend/src/shared/api/*` -- run `npm run generate:api` after a Host build and commit the result: NSwag emits a new `JobPostingsClient` (the `JobPostings_Create` operation → a `create(...)` method) and its `CreateJobPostingRequest` / `JobPostingResponse` interfaces, plus a barrel re-export in `frontend/src/shared/api/index.ts` if the generator adds a new file. No hand edits; a second `generate:api` run must leave `git status` clean.
- `frontend/src/shared/api/index.ts` -- if NSwag produces the new client in the same `nexus-api-client.ts`, no change; if a new file appears, re-export it from the barrel (hand-written, not ignored) so 2.1b imports `shared/api`.

**Reference — do not change**
- `backend/NexusJob.Modules.Identity/**` (the filters being copied), `backend/NexusJob.ArchitectureTests/**` (already enumerate JobPostings; must stay green), `backend/NexusJob.Modules.JobPostings.Contracts/**` (stays empty — Contracts are 2.2), `docker-compose.yml` / `.github/workflows/ci.yml` (the `backend` job already runs `dotnet test`; the `openapi-client` job already regenerates + drift-checks; no change needed).
- `_bmad-output/implementation-artifacts/spec-1-3a-identity-backend-and-auth-endpoints.md` -- the template: `MapGroup` + slice folders, the two endpoint filters, the `PostgresErrorCodes` catch pattern (not needed here — no unique constraint), the `WithName` operation-id convention, the `IdentityApiFixture` / `AuthApiClient` test helpers, and the cookie/claim assertions the new tests mirror.

## Tasks & Acceptance

**Execution:**
- [x] `backend/NexusJob.Modules.JobPostings/NexusJob.Modules.JobPostings.csproj` -- add the EF Core + Npgsql package references.
- [x] `backend/NexusJob.Modules.JobPostings/Persistence/{JobPosting,JobPostingsDbContext,JobPostingsDbContextFactory}.cs` -- entity + `public` context (default schema `job_postings`, `job_posting` snake_case) + design-time factory.
- [x] `backend/NexusJob.Modules.JobPostings/Persistence/Migrations/*` -- `dotnet ef migrations add InitialJobPostings`; verify it creates only `job_postings.job_posting` + history in `job_postings`.
- [x] `backend/NexusJob.Modules.JobPostings/Auth/{AntiforgeryEndpointFilter,DataAnnotationsValidationFilter,CompanyOnlyEndpointFilter}.cs` -- the two copied filters + the Company-only `403` filter (local `account_type` / `company` constants).
- [x] `backend/NexusJob.Modules.JobPostings/Features/CreateJobPosting/*` -- request (+ DataAnnotations), response, endpoint delegate, handler (Guid v7 id, owner from `NameIdentifier`, trimmed title/description, `DateTimeOffset.UtcNow`, single `SaveChanges`).
- [x] `backend/NexusJob.Modules.JobPostings/JobPostingsModule.cs` -- `AddJobPostingsModule` registers the context (SearchPath + history table + placeholder fallback) and the handler/filters; `MapJobPostingsModule` maps `POST /api/job-postings` with `RequireAuthorization` + the three filters + `Produces`/`ProducesProblem` metadata.
- [x] `backend/NexusJob.Host/Program.cs` -- add `JobPostingsDbContext` to the try/caught startup-migration block; add the `using`.
- [x] `backend/NexusJob.IntegrationTests/{TestSupport.cs,JobPostingsEndpointsTests.cs,OpenApiDocumentTests.cs}` -- `AuthApiClient.CreatePostingAsync` + a `JobPostingsDatabase` helper; one test per I/O-matrix row; extend the OpenAPI doc assertion for `/api/job-postings`.
- [x] `frontend/src/shared/api/*` -- `npm run generate:api` after a Host build; commit the regenerated client (no hand edits; drift-clean on a re-run).

**Acceptance Criteria:**
- Given a clean `job_postings` schema, when the `InitialJobPostings` migration is applied, then a `job_posting` table exists with `id` (uuid PK), `owner_company_id` (uuid, no FK), `title`, `description`, `created_at` (timestamptz), and the history table is `job_postings.__EFMigrationsHistory`.
- Given `dotnet build backend/NexusJob.sln` and `dotnet test backend/NexusJob.sln`, when they run, then the build is warning-free and every suite passes — the ArchUnitNET boundary gates (JobPostings still references only `.Contracts`; no cross-schema raw SQL), the unchanged `HealthEndpointTests` / Identity `AuthEndpointsTests`, and the new `JobPostingsEndpointsTests`.
- Given `AddJobPostingsModule` / `MapJobPostingsModule`, when the Host wires them, then the Host contains no JobPostings-specific code beyond the two calls and the one added `Migrate()` line, and `NexusJob.Modules.JobPostings.Contracts` still declares no types.
- Given a Host build then `cd frontend && npm run generate:api`, when it runs, then `frontend/src/shared/api/` gains the `POST /api/job-postings` client and a second run leaves `git status` clean; `npm run lint`, `npm test`, and `npm run build` still pass with the regenerated client present.
- Given any `/api/job-postings` request in the new tests, when it completes, then no cookie value, `X-CSRF-TOKEN`, or request-body content appears in the structured logs.

## Implementation Notes

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-07) — blind-hunter, edge-case-hunter, verification-gap

No intent_gap or bad_spec — no loopback. 7 `patch` entries (one small entity/migration change + a `Program.cs` reorder + test hardening), 1 consolidated `defer`, the rest rejected. Local re-verification (`dotnet build` 0W/0E, `dotnet test` = Architecture 22, Host 3, IntegrationTests 54; frontend `lint` + all gates + 87 tests + `build`; `generate:api` idempotent) was green before the pass.

**patch:**
- `backend/NexusJob.Host/Program.cs` (edge-case-hunter, high-confidence claim) — the new `JobPostingsDbContext.Migrate()` was inserted **between** the `IdentityDbContext` and `DataProtectionKeysDbContext` calls; the spec Code Map / Design Notes said "after the two existing calls". As placed, a JobPostings-migration failure in the shared `try` now also skips the stable Data-Protection-keys migration. low. Fix: move the new line to after `DataProtectionKeysDbContext.Migrate()`.
- `JobPostingsDbContext.OnModelCreating` + the `InitialJobPostings` migration (blind ×2) — `title` / `description` map to unbounded `text` while `CreateJobPostingRequest` caps them at `[StringLength(200)]` / `[StringLength(4000)]` (DB constraint ≠ app constraint), and there is no index on `owner_company_id` (the natural filter column for later "my postings" / summary reads). low. Fix (the migration is not yet applied anywhere): add `.HasMaxLength(200)` / `.HasMaxLength(4000)` and `entity.HasIndex(e => e.OwnerCompanyId)`, regenerate `InitialJobPostings` + the model snapshot, and update `InitialJobPostings_migration_…`'s column-type assertions (`character varying`) + add an index assertion.
- `JobPostingsEndpointsTests` (blind, verification-gap other) — no test exercises an over-length title / description; the just-added `[StringLength]` caps are unpinned. low. Fix: a case with a 201-char title / 4001-char description → `400` naming the field, no row.
- `JobPostingsEndpointsTests` (blind) — tenant isolation is only implied by the `owner_company_id` filter, never directly verified. low-medium. Fix: register Company A + create, register Company B + create, assert each owner's count is 1 and Company A's posting row carries A's id.
- `JobPostingsEndpointsTests` happy path (blind) — the spec's "no envelope, no `Location`" is not asserted. low. Fix: `Assert.Null(response.Headers.Location)` and assert the `200` JSON body has exactly `{ id, title, description, createdAt }` (4 properties, no extras).
- `JobPostingsEndpointsTests` happy path (verification-gap other) — `id` is not verified to be a v7 GUID (a regression to `Guid.NewGuid()` would pass). low. Fix: `Assert.Equal(7, postingId.Version)`.
- `JobPostingsEndpointsTests` log-redaction test (edge-case-hunter) — the test can pass vacuously if `fixture.Logs.Snapshot()` is empty. low. Fix: also assert the snapshot contains a line mentioning `/api/job-postings`, proving the redaction loop inspected the create's request line.

**defer:**
- The JobPostings module verbatim-copies cross-cutting pieces from Identity — `AntiforgeryEndpointFilter`, `DataAnnotationsValidationFilter<TRequest>`, and the `Build*ConnectionString` schema-forcing pattern — with no shared kernel and no parity test, so the two modules can drift silently (blind, edge-case-hunter). Also raised: antiforgery-validation failures are never logged (a security-observability gap, pre-existing in Identity's copy), and `Build*ConnectionString`'s `catch when (e is ArgumentException or FormatException)` lets any other builder exception abort Host startup. → `deferred-work.md`: extract a shared kernel for these filters + the connection-string helper so Identity and JobPostings share one implementation, and while doing so add antiforgery-rejection logging and widen the connection-string `catch`.

**rejected:**
- blind — the defensive `!Guid.TryParse(NameIdentifier)` → `401` branch in `CreateJobPostingHandler` is silent: mirrors Identity's `GetMeHandler`; the branch is unreachable given `RequireAuthorization()` + `CompanyOnlyEndpointFilter` + a `ClaimsPrincipalFactory`-issued, Data-Protection-sealed cookie. A warning log on a can't-happen path is noise. low.
- blind — `DataAnnotationsValidationFilter` is misfiled under `Auth/`: it sits exactly where Identity's copy sits (`NexusJob.Modules.Identity/Auth/`); moving it in JobPostings alone diverges the two. Cosmetic, repo-wide.
- blind — `CreateJobPostingRequest` / `JobPostingResponse` are `public` while the module is otherwise `internal`: mirrors Identity's `RegisterRequest` / `AuthAccountResponse`; the request/response DTOs must be `public` for `Microsoft.AspNetCore.OpenApi` schema generation. The `DbContext` / entity / `DbSet` are correctly `internal`.
- blind — the three copied filters have no parity test keeping them in sync with Identity's: each copy is behaviorally tested independently (`AuthEndpointsTests` vs `JobPostingsEndpointsTests`); a cross-implementation parity assertion is brittle. The sync risk is eliminated by the shared-kernel `defer` above.
- blind / edge-case-hunter — no request-body size guard / no `415` documented: Kestrel's default `MaxRequestBodySize` (30 MB) is the backstop and `[StringLength]` rejects fast; the generated client always sends `application/json`, so `415` is only reachable by a non-conforming caller and an opaque error there is acceptable. Consistent with the shipped Identity endpoints.
- blind — `CompanyOnlyEndpointFilter` doesn't check `IsAuthenticated` and doesn't reject a `Guid.Empty` owner: `RequireAuthorization()` is present and the `account_type` claim / cookie are integrity-protected; both scenarios require breaking established upstream guards. `Guid.Empty` is unreachable (`ClaimsPrincipalFactory` never issues it). Mirrors `GetMeHandler` / `RegisterHandler`.
- blind — the `IdentityApiFixture` / `IdentityApiCollection` name is misleading now that JobPostings reuses it: the reuse is a spec Resolved Decision ("do not rename the existing one"); a rename is cosmetic churn across shipped test files.
- edge-case-hunter — generated `create()` resolves to `null` on a `200` with an empty body: the standard NSwag-generated pattern (identical across every client method); the backend's `Results.Ok(new JobPostingResponse(...))` always serialises a non-empty body; `nexus-api-client.ts` is generated + `eslint`-ignored and not hand-editable.
- edge-case-hunter — a malformed connection string that makes `NpgsqlConnectionStringBuilder` throw a non-Arg/Format exception aborts Host startup: a verbatim mirror of the shipped `IdentityModule.BuildIdentityConnectionString`; such a string also breaks the migration (caught) and every DB op. Folded into the shared-kernel `defer`.
- verification-gap (other) — the empty-body `400`'s `{ errors: { body: [...] } }` shape is not asserted: no in-repo consumer reads `errors.body`; the two row-6 tests assert the `400` + no-row outcome the I/O matrix requires; identical assertion level to the shipped `AuthEndpointsTests.Register_with_an_empty_body_returns_400_not_500`.

## Design Notes

- **Why two more filter copies.** The modular monolith keeps each module self-contained (AD-1 / AD-14): a module cannot reference another module's implementation, and there is no shared-kernel project. Identity's `AntiforgeryEndpointFilter` and `DataAnnotationsValidationFilter<T>` are `internal`, so JobPostings gets its own ~20-line copies. This is deliberate duplication; a shared-kernel extraction is a tracked future refactor, not this story.
- **Company-only vs unauthenticated.** `RequireAuthorization()` yields `401` for a missing/invalid cookie. The `CompanyOnlyEndpointFilter` runs after it and yields `403` when the authenticated principal's `account_type` claim is not `company` — so a Job Seeker who is genuinely signed in is *forbidden*, not *unauthenticated*. The claim name/value are the cookie-auth wire contract (AD-13); JobPostings hardcodes the two strings rather than referencing Identity's `internal AccountType`.
- **`created_at` type.** Use `DateTimeOffset` end to end (`timestamptz` in Npgsql, ISO-8601 on the wire). Do not store `DateTime` with `Kind=Unspecified`.
- **No `PostgresException`/`23505` catch** — `job_posting` has no unique constraint (a Company may post many identical-looking jobs in v1). A `SaveChanges` failure is a genuine `500` and should surface as one.
- **Startup migration ordering.** Add `JobPostingsDbContext.Migrate()` inside the existing `try` after the Identity and Data-Protection contexts; a failure there is caught and logged exactly like the others, so `HealthEndpointTests` (Host starts DB-down) stay green.
- **OpenAPI drift is a real gate.** Adding the endpoint changes the build-time `openapi/NexusJob.Host.json`, which makes the committed `frontend/src/shared/api/` stale — the CI `openapi-client` job fails until it is regenerated and committed. That regeneration is in-scope here even though no frontend code consumes the client yet.

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings, 0 errors.
- `dotnet ef migrations list --project backend/NexusJob.Modules.JobPostings` -- expected: lists `InitialJobPostings`.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass, including `JobPostingsEndpointsTests` (Testcontainers `postgres:18`; Docker must be running) and the unchanged Identity / Architecture / Host suites.
- `docker compose up`, then: `GET /api/auth/csrf` → `POST /api/auth/register {accountType:"company",…}` → `POST /api/job-postings {title,description}` with the token → expect `200 { id, title, description, createdAt }`; repeat the create with a `job_seeker` session → `403`; with no cookie → `401`; `docker compose logs app` shows no cookie / token / body.
- `dotnet run --project backend/NexusJob.Host` then `curl -s localhost:2052/openapi/v1.json | jq '.paths["/api/job-postings"]'` -- expected: a `post` operation with the `200` `{id,title,description,createdAt}` schema and `400` / `401` / `403` problem responses.
- `cd frontend && npm ci && npm run generate:api && git status --porcelain src/shared/api` -- expected: the client gains the new operation on the first run; empty on a re-run.
- `npm run lint && npm test -- --run && npm run build` -- expected: all green with the regenerated client present.

**Manual checks:**
- Inspect `job_postings.job_posting` after a create: one row, `owner_company_id` equals the registering Company's account id, `created_at` is a recent UTC `timestamptz`.
- Confirm `job_postings.__EFMigrationsHistory` exists and `identity` / `public` schemas are untouched by the new migration.
