---
title: 'Identity backend: persistence and the /api/auth/* endpoints'
type: 'feature'
created: '2026-09-06'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '6848dbae617c7194fc1606ffc167908d5a1aab7f'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Identity module is a no-op stub. There is no persistence, no auth, and no `/api/auth/*` surface, so a Company cannot register or sign in and no later story that needs a session can proceed.

**Approach:** Give Identity its own `identity`-schema persistence (`CompanyAccount`) with an EF Core 10 / Npgsql `DbContext` and its own migration history, and implement the Company half of the auth surface: `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/logout`, `GET /api/auth/csrf`. Add the app-wide auth infrastructure to the Host (cookie scheme, antiforgery, Data-Protection key persistence, startup migration). Cover every behaviour with Testcontainers integration tests against real Postgres. This is story 1.3a; the OpenAPI 3.0 document emission, the OpenAPI→NSwag client pipeline, and the frontend Sign up / Log in surface are 1.3b (see `deferred-work.md`).

## Boundaries & Constraints

**Always:**
- Follow the architecture ADs verbatim: AD-3 (feature-slice folders `Modules/Identity/Features/<VerbNoun>/`, no Domain/App/Infra split), AD-5 (`DbContext` maps only Identity's entities; `search_path`/default schema = `identity`), AD-7 (Identity owns its own EF migration history), AD-10 (Host is the only composition root; Identity exposes only `AddIdentityModule` / `MapIdentityModule`), AD-11 (`CompanyAccount` table, email unique **within the table**), AD-13 (cookie auth, `PasswordHasher<T>` PBKDF2 with `IterationCount` ≥ 600000 from config, `HttpOnly; Secure; SameSite=Lax`, claims `NameIdentifier` = account id + `account_type` = `company`, identity read only from `NameIdentifier`, CSRF double-submit `X-CSRF-TOKEN` seeded by `GET /api/auth/csrf`, credentials/hashes/cookies never logged), AD-14 (endpoint delegates call slice handler classes resolved from DI — no mediator), AD-15 (RFC 9457 ProblemDetails on every non-2xx; success returns the representation directly — the OpenAPI **document** itself is 1.3b), AD-22 (Data-Protection keys in `public` schema owned by the Host; all config/secrets from environment / .NET configuration).
- Identifiers: `Guid` generated in app code with `Guid.CreateVersion7()`, `Guid` in C# / string only at the JSON edge. DB identifiers `snake_case`; columns `id`, `email`, `password_hash`, `display_name`.
- One write request = one module, one `SaveChanges` (AD-8): register touches only `identity`.
- `TreatWarningsAsErrors` stays on; `dotnet build` and the full `dotnet test` (architecture tests + Host tests + the new integration tests) are green. The AD-2 boundary gates (ArchUnitNET) must still pass with the new module code.
- Every row of the I/O matrix is covered by an integration test that runs against a real Postgres (Testcontainers) and passes.

**Never:**
- No Job Seeker path — `accountType` other than `company` is rejected with ProblemDetails; `job_seeker_account`, its table, and its register/login branch are story 1.4.
- No OpenAPI document emission (`AddOpenApi` / `MapOpenApi`), no frontend, no NSwag client generation, no generated `shared/api`, no Vite proxy — all story 1.3b. Endpoints still carry accurate `Produces` / `Accepts` / status-code metadata so 1.3b's document is correct, but nothing serves a document in 1.3a.
- No full ASP.NET Core Identity framework, no `IdentityDbContext`/`UserManager`; Identity owns its own table and register/login logic (AD-13).
- No `IIdentityApi` Contract implementation or DTOs in `NexusJob.Modules.Identity.Contracts` — that surface is consumed only in Epics 2–3 (epic context, Cross-Story Dependencies); `.Contracts` stays empty here.
- No cross-schema access; no foreign key of any kind in this story (single table).
- No mediator library, no FluentValidation (use built-in .NET 10 minimal-API validation / DataAnnotations), no domain-event bus.
- No secret, connection string, or iteration count committed to the repo or baked into the image.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Register a new Company | `POST /api/auth/register` `{accountType:"company", name, email, password}`, valid antiforgery token, email not present | `200` with `{ id, accountType:"company", displayName:name }`; `Set-Cookie` auth cookie (`HttpOnly; Secure; SameSite=Lax`) carrying `NameIdentifier` = new id + `account_type=company`; exactly one `company_account` row, `password_hash` a PBKDF2 hash (never the plaintext), single `SaveChanges` in `identity` | N/A |
| Register, email already a Company | same, email already in `company_account` (case-insensitively) | `409` RFC 9457 ProblemDetails; no second row; no cookie issued | ProblemDetails, generic title; does not echo the password |
| Register, `accountType` not `company` | `{accountType:"job_seeker", …}` | `400` ProblemDetails ("job seeker registration is not available yet"); no row | ProblemDetails |
| Register, invalid body | missing/invalid `email`, empty `password` (< 8 chars), empty `name` | `400` validation ProblemDetails naming the invalid fields; no row | ProblemDetails (validation) |
| Login, correct credentials | `POST /api/auth/login` `{accountType:"company", email, password}`, antiforgery token, matching row | `200` with `{ id, accountType:"company", displayName }`; same cookie shape as register | N/A |
| Login, wrong password or unknown email | matching-shape body, no row or hash mismatch | one `401` generic ProblemDetails — identical body for "no such email" and "wrong password"; no cookie | ProblemDetails, no field-level detail |
| `GET /api/auth/me`, signed in | valid auth cookie | `200` `{ id, accountType:"company", displayName }`, resolved from `NameIdentifier` only | N/A |
| `GET /api/auth/me`, no/invalid cookie | no cookie | `401` (ProblemDetails) | ProblemDetails |
| `POST /api/auth/logout` | valid auth cookie + antiforgery token | `204`; response clears the auth cookie; a subsequent `/api/auth/me` is `401` | N/A |
| `GET /api/auth/csrf` | anonymous | `200` `{ token }`; sets the antiforgery cookie; `AllowAnonymous`, no token required | N/A |
| State-changing `/api/auth/*` without antiforgery | `POST` register/login/logout, missing/invalid `X-CSRF-TOKEN` | `400` (antiforgery failure), ProblemDetails; no state change | ProblemDetails |
| Structured logs during any of the above | any request | one JSON log line per request (method/path/status/duration only); no `Set-Cookie`, `X-CSRF-TOKEN`, email, password, or hash value anywhere in the logs | N/A |
| Host starts with the database reachable | `ConnectionStrings__Postgres` valid, schema empty or behind | both `IdentityDbContext` and `DataProtectionKeysDbContext` pending migrations are applied before the app serves traffic; `/health` is `200` | N/A |
| Host starts with the database down / misconfigured | connection string unreachable, malformed, or absent | the Host still starts; the startup migration failure is logged once (no crash); `/health` reports `503` until the database is reachable and migrations apply on a later restart | migration exception caught + logged; process does not exit non-zero |

## Resolved Decisions

- **Migrations run automatically at Host startup, unconditionally** — between `builder.Build()` and `app.Run()`, in a DI scope, call `Database.Migrate()` for `IdentityDbContext` then `DataProtectionKeysDbContext`. **The whole block is wrapped in try/catch**: a failure is logged once (`LogError`) and the Host still starts, so the story-1.1 invariant "the Host starts even when the database is down and `/health` reports unhealthy" is preserved and the existing `HealthEndpointTests` keep passing. No `--migrate` switch, no `docker-compose` migrate service. Integration tests get migration for free — `WebApplicationFactory<Program>` boot runs the same path against the Testcontainer.
- **The OpenAPI 3.0 document is not part of 1.3a.** `AddOpenApi` / `MapOpenApi` (pinned to 3.0 output), the `Microsoft.AspNetCore.OpenApi` package, and the "serves one OpenAPI document" acceptance criterion move to story 1.3b, alongside the NSwag client generation that consumes the document. 1.3a endpoints still declare correct `Produces`/`Accepts`/status metadata so 1.3b's generated document is accurate on day one.

</frozen-after-approval>

## Code Map

- `backend/Directory.Packages.props` -- add `Microsoft.EntityFrameworkCore` + `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` (all v10 line), `Testcontainers.PostgreSql` (~4.x). Pin exact versions. (`Microsoft.AspNetCore.OpenApi` is 1.3b.)
- `backend/NexusJob.Modules.Identity/NexusJob.Modules.Identity.csproj` -- add the EF Core + Npgsql package refs (keep the `FrameworkReference` and the `.Contracts` project ref; no new project refs).
- `backend/NexusJob.Modules.Identity/IdentityModule.cs` -- flesh out `AddIdentityModule` (register `IdentityDbContext` via `AddDbContext` with `UseNpgsql(connString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))` + `SearchPath=identity`; `PasswordHasher<CompanyAccount>` with `IterationCount` from `Auth:PasswordHasher:IterationCount` (default 600000); the five slice handler classes) and `MapIdentityModule` (a `MapGroup("/api/auth")` mapping the five endpoints).
- `backend/NexusJob.Modules.Identity/Persistence/CompanyAccount.cs` -- NEW. Entity: `Guid Id`, `string Email`, `string PasswordHash`, `string DisplayName`.
- `backend/NexusJob.Modules.Identity/Persistence/IdentityDbContext.cs` -- NEW. `HasDefaultSchema("identity")`; `company_account` table, snake_case columns, unique index on `email`; `DbSet<CompanyAccount>`.
- `backend/NexusJob.Modules.Identity/Persistence/Migrations/` -- NEW. Initial migration creating `identity.company_account` + the unique index (history table `identity.__EFMigrationsHistory`).
- `backend/NexusJob.Modules.Identity/Features/Register/` -- NEW. `RegisterEndpoint` (delegate), `RegisterHandler`, `RegisterRequest` (`accountType`, `name`, `email`, `password` + DataAnnotations), `AuthAccountResponse` (shared `{ id, accountType, displayName }`).
- `backend/NexusJob.Modules.Identity/Features/Login/` -- NEW. `LoginEndpoint`, `LoginHandler`, `LoginRequest`. Reuses `AuthAccountResponse`.
- `backend/NexusJob.Modules.Identity/Features/GetMe/` -- NEW. `GetMeEndpoint` (reads `ClaimTypes.NameIdentifier`), `GetMeHandler`.
- `backend/NexusJob.Modules.Identity/Features/Logout/` -- NEW. `LogoutEndpoint` (`SignOutAsync`).
- `backend/NexusJob.Modules.Identity/Features/GetCsrfToken/` -- NEW. `GetCsrfTokenEndpoint` (`IAntiforgery.GetAndStoreTokens`).
- `backend/NexusJob.Modules.Identity/Auth/` -- NEW. `AuthCookie` constants (scheme + cookie name), `ClaimsPrincipalFactory` (builds the principal from `(id, accountType, displayName)`), `AccountType` helper. The handlers call `httpContext.SignInAsync(AuthCookie.Scheme, principal)` / `SignOutAsync`.
- `backend/NexusJob.Host/Program.cs` -- add: `AddAuthentication(AuthCookie.Scheme).AddCookie(…)` with `HttpOnly; Secure; SameSite=Lax` + a 401/JSON `OnRedirectToLogin`/`OnRedirectToAccessDenied` (never an HTML redirect for `/api/*`); `AddAuthorization()`; `AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN")`; `AddDataProtection().PersistKeysToDbContext<DataProtectionKeysDbContext>()`; `AddDbContext<DataProtectionKeysDbContext>`; `app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();` after `UseHttpLogging`, before the module maps; and a try/caught startup-migration block (scope → `IdentityDbContext.Database.Migrate()` → `DataProtectionKeysDbContext.Database.Migrate()`; on exception `app.Logger.LogError` and continue). Pass the raw connection string (or a non-functional placeholder when it is absent) to `UseNpgsql` so an unconfigured Host still boots.
- `backend/NexusJob.Host/Persistence/DataProtectionKeysDbContext.cs` -- NEW. Maps the Data-Protection `DataProtectionKey` entity to `public.data_protection_keys`; own migration history in `public`.
- `backend/NexusJob.Host/Persistence/Migrations/` -- NEW. Initial migration for `public.data_protection_keys`.
- `backend/NexusJob.Host/NexusJob.Host.csproj` -- add `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`. (OpenAPI package is 1.3b.)
- `backend/NexusJob.Host/appsettings.json` -- add an `Auth` section documenting `PasswordHasher:IterationCount` (value still comes from env in real deployments).
- `backend/NexusJob.IntegrationTests/` -- NEW test project (`IsTestProject`, refs `NexusJob.Host`). `PostgresFixture` (Testcontainers `PostgreSqlContainer` on `postgres:18`, migrates both contexts), `WebApplicationFactory<Program>` override injecting the container connection string + an in-test iteration count (e.g. 10000, to keep hashing fast), `AuthEndpointsTests` covering every I/O-matrix row. Add to `NexusJob.sln`.
- `backend/NexusJob.sln` -- add `NexusJob.IntegrationTests` (the new Identity persistence/migration files compile via the existing `NexusJob.Modules.Identity` project — no sln change for those).
- `docker-compose.yml` -- no change needed (startup migration means the `app` service just needs its existing `ConnectionStrings__Postgres`; `depends_on: postgres healthy` already gates it).
- `.github/workflows/ci.yml` -- the `backend` job's existing `dotnet test` picks up `NexusJob.IntegrationTests`; ubuntu-latest has Docker for Testcontainers. Bump the `backend` job `timeout-minutes` if the `postgres:18` pull + hashing pushes it near 15.
- `backend/NexusJob.ArchitectureTests/RawSqlSchemaScan.cs`, `ModuleAssemblies.cs`, `BoundaryRules.cs` -- read-only reference; the new Identity code must keep them green (no raw cross-schema SQL, module still only refs `.Contracts`).
- `_bmad-output/implementation-artifacts/spec-1-2-design-system-app-shell.md` -- continuity: FSD/token patterns are frontend-only; no carry-over into 1.3a beyond the shared CI file.

## Tasks & Acceptance

**Execution:**
- [x] `backend/Directory.Packages.props` -- pin EF Core 10, Npgsql EF provider, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Testcontainers.PostgreSql`.
- [x] `backend/NexusJob.Modules.Identity/NexusJob.Modules.Identity.csproj` + `backend/NexusJob.Host/NexusJob.Host.csproj` -- add the package references each needs.
- [x] `backend/NexusJob.Modules.Identity/Persistence/CompanyAccount.cs` + `IdentityDbContext.cs` -- entity + context: default schema `identity`, `company_account` snake_case, unique index on `email`.
- [x] `backend/NexusJob.Modules.Identity/Persistence/Migrations/*` -- `dotnet ef migrations add InitialIdentity` for `IdentityDbContext` (history table in `identity`).
- [x] `backend/NexusJob.Modules.Identity/Auth/*` -- `AuthCookie` constants, claims-principal factory, `AccountType`.
- [x] `backend/NexusJob.Modules.Identity/Features/Register/*` -- endpoint + `RegisterHandler` (Guid v7 id, lowercase-normalise email, `PasswordHasher.HashPassword`, reject non-`company` and duplicate email, `SignInAsync`, single `SaveChanges`) + request/response + DataAnnotations (`[Required][EmailAddress]` email, `[Required][MinLength(8)]` password, `[Required]` name).
- [x] `backend/NexusJob.Modules.Identity/Features/Login/*` -- endpoint + `LoginHandler` (lookup by normalised email; `PasswordHasher.VerifyHashedPassword`; one generic `401` for any mismatch; `SignInAsync` on success) + request.
- [x] `backend/NexusJob.Modules.Identity/Features/GetMe/*` -- endpoint (`RequireAuthorization`) + handler returning `{ id, accountType, displayName }` from `NameIdentifier` (+ a `company_account` read for `displayName`).
- [x] `backend/NexusJob.Modules.Identity/Features/Logout/*` -- endpoint (`RequireAuthorization`, `SignOutAsync`, `204`).
- [x] `backend/NexusJob.Modules.Identity/Features/GetCsrfToken/*` -- endpoint (`AllowAnonymous`, `GetAndStoreTokens`, `{ token }`).
- [x] `backend/NexusJob.Modules.Identity/IdentityModule.cs` -- `AddIdentityModule` registers the context, `PasswordHasher<CompanyAccount>` (iteration count from config, default 600000), and the handlers; `MapIdentityModule` maps the `/api/auth` group with per-endpoint auth + antiforgery metadata.
- [x] `backend/NexusJob.Host/Persistence/DataProtectionKeysDbContext.cs` + `Migrations/*` -- `public.data_protection_keys`, own migration history.
- [x] `backend/NexusJob.Host/Program.cs` -- cookie auth scheme (JSON `401`, no HTML redirect for `/api/*`), `AddAuthorization`, antiforgery (`X-CSRF-TOKEN`), Data-Protection→`DataProtectionKeysDbContext`, middleware order (`UseAuthentication`/`UseAuthorization`/`UseAntiforgery` after `UseHttpLogging`, before the module maps), and the try/caught unconditional startup migration for both contexts.
- [x] `backend/NexusJob.Host/appsettings.json` -- `Auth:PasswordHasher:IterationCount` documented default.
- [x] `.github/workflows/ci.yml` -- confirm the `backend` job runs `NexusJob.IntegrationTests`; bump `timeout-minutes` if needed.
- [x] `backend/NexusJob.IntegrationTests/*` + `backend/NexusJob.sln` -- new project: `PostgresFixture` (Testcontainers `postgres:18`, migrate both contexts), factory override (container conn string + fast iteration count), `AuthEndpointsTests` with one test per I/O-matrix row.

**Acceptance Criteria:**
- Given a clean `identity` schema, when the Identity migration is applied, then a `company_account` table exists with `id` (uuid PK), `email`, `password_hash`, `display_name` and a unique index on `email`, and the migration-history table is `identity.__EFMigrationsHistory`.
- Given `dotnet build backend/NexusJob.sln` and `dotnet test backend/NexusJob.sln`, when they run, then the build is warning-free and every test passes, including the ArchUnitNET boundary gates, the existing `HealthEndpointTests` (unchanged — the Host still starts with the DB unreachable), and the new Testcontainers integration tests.
- Given any `/api/auth/*` request, when it completes, then no `Set-Cookie` value, `X-CSRF-TOKEN`, email, password, or password-hash string appears in the structured logs.
- Given `AddIdentityModule` and `MapIdentityModule`, when the Host wires them, then the Host contains no other Identity-specific code beyond the two calls and the app-wide auth infrastructure (cookie scheme, antiforgery, Data-Protection, startup migration) named in AD-13/AD-22.
- Given the `.Contracts` project, when this story completes, then it still declares no `IIdentityApi` and no DTOs.

## Implementation Notes

- **Validation is an explicit endpoint filter, not `AddValidation()` source-gen.** The request DTOs live in the Identity module (a `Microsoft.NET.Sdk` class library); the .NET 10 minimal-API validation source generator only scans the web SDK project, so `AddValidation()` alone left register/login bodies unvalidated (returned `200`). Enforcement is `DataAnnotationsValidationFilter<TRequest>` (an `IEndpointFilter` running `System.ComponentModel.DataAnnotations.Validator.TryValidateObject` -> `Results.ValidationProblem`). `AddValidation()` is still called in the Host (epic guidance, forward-compatible if DTOs ever move).
- **Antiforgery on JSON endpoints is an endpoint filter too.** `app.UseAntiforgery()` only auto-validates form-data endpoints; `AntiforgeryEndpointFilter` calls `IAntiforgery.ValidateRequestAsync` and maps `AntiforgeryValidationException` -> `400` ProblemDetails on register/login/logout.
- **`IdentityDbContext` is `public`** so the Host composition root can call `Database.Migrate()` on it at startup (per the Resolved Decision wording). Its `DbSet<CompanyAccount>` and the entity stay `internal`, so the module surface is unchanged in practice (no other project can reference the assembly, AD-2).
- **`accountType != "company"`:** register -> `400` with detail "Job seeker registration is not available yet."; login -> the same generic `401` as any credential mismatch (login must not leak which field/branch failed).
- **ProblemDetails `title` strings are generic, stable, non-UI:** login failure `"Invalid credentials."` (no `detail`); duplicate register `"Email is already registered."`. End-user copy ("...Please try again.", etc.) is story 1.3b's concern, not the API.
- **Login has no timing side-channel:** on "unknown email" the handler still runs one `VerifyHashedPassword` against a fixed 600k-iteration dummy hash before returning the same generic `401`, so it does the same constant work as the "wrong password" path.
- **Login `401` bodies differ only by the per-request `traceId` extension** (from `AddProblemDetails()` + `Activity`). Same `title`, `status`, and no `detail`/`errors` for "unknown email" and "wrong password" - the integration test asserts that rather than raw byte-equality.
- **Null/empty JSON body -> `400`:** `DataAnnotationsValidationFilter<T>` returns a `{ "body": ["A request body is required."] }` ValidationProblem when the bound argument is null, and `EmailNormalizer.Normalize` guards with `ArgumentNullException.ThrowIfNull`.
- **Auth-config typos boot unhealthy, not crash:** the `PasswordHasher` iteration count is read with `int.TryParse` (falls back to 600000 on a non-integer), matching how a bad connection string is tolerated.
- **Integration tests run the app at `https://localhost`** (in-memory `TestServer`, no real TLS) so the `Secure` auth cookie is stored and replayed by the client exactly as a browser would; `CookieSecurePolicy.Always` is unchanged.
- **DB-down caveat:** with the database unreachable, `GET /api/auth/csrf` returns `500` (antiforgery token generation needs the Data-Protection key ring, which is DB-backed). The Host still starts and `/health` reports `503`, which is all the I/O matrix row requires; the SPA (1.3b) calls `/api/auth/csrf` only when the app is healthy.

## Verification Results

- `dotnet build backend/NexusJob.sln --configuration Release` -- 0 warnings, 0 errors (clean build).
- `dotnet test backend/NexusJob.sln --configuration Release` -- all pass: `NexusJob.ArchitectureTests` 22/22 (AD-2 boundary gates green with the new EF-referencing module code), `NexusJob.Host.Tests` 3/3 (Host still starts DB-down), `NexusJob.IntegrationTests` 22/22 (Testcontainers `postgres:18`, one test per I/O-matrix row + login email-normalization, invalid-cookie `/me`, empty-body `400`, and the `PasswordHasher` default-iteration-count assertion).
- `dotnet ef migrations list` -- `NexusJob.Modules.Identity` lists `20260906173942_InitialIdentity`; `NexusJob.Host` (`DataProtectionKeysDbContext`) lists `20260906173952_InitialDataProtectionKeys`.

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-07) — blind-hunter, edge-case-hunter, verification-gap

No intent_gap or bad_spec — no loopback. Nine `patch` entries applied by the step-03 subagent, one `defer`, the rest rejected.

**patch:**
- `LoginHandler` — **timing side-channel**: `account is null` returns immediately, skipping the ~600k-iteration PBKDF2 verify done for a wrong password, so "unknown email" answers measurably faster and defeats the frozen intent that the two be indistinguishable. medium. Fix: run a fixed dummy-hash `VerifyHashedPassword` when the account is missing (constant work).
- `LoginHandler` (verification-gap, pre-verified) — **login-side email normalisation is unverified**: every login test uses lowercase `Guid:N` fixtures, so `EmailNormalizer.Normalize` in `LoginHandler` is a no-op that could be deleted green; a Company that registered with a mixed-case email would get a permanent generic `401`. medium. Fix: add a `[Fact]` that registers with `Mixed@Case.Example.com` and logs in with the lower-cased spelling, asserting `200` + one `nexusjob_auth` cookie.
- `LoginHandler` + `RegisterHandler` — **RFC 9457 `title` carries end-user UI copy** ("That email and password don't match. Please try again.", "This email is already registered.") which is (a) not a stable non-UI summary and (b) 1.3b's presentation copy leaking into the 1.3a API contract. low-medium. Fix: generic stable titles ("Invalid credentials.", "Email is already registered."), no "Please try again.", login stays detail-free.
- `DataAnnotationsValidationFilter` + `EmailNormalizer` — **null/empty JSON body → `500` not `400`**: the filter's `if (request is not null)` falls through to the handler, which dereferences `request.AccountType`; `EmailNormalizer.Normalize` also has no null guard unlike the rest of the codebase. low-medium. Fix: filter returns a `400` ValidationProblem when the bound request is null; `ArgumentNullException.ThrowIfNull` in `EmailNormalizer`.
- `IdentityModule.AddIdentityModule` — **`configuration.GetValue<int?>` throws before `Build()`** on a non-integer `Auth__PasswordHasher__IterationCount`, outside the startup-migration try/catch, so an Auth-config typo crashes the Host (inconsistent with the connection-string path, which boots unhealthy). low. Fix: `int.TryParse`, fall back to the 600000 default with a warning log.
- `RegisterRequest` + `LoginRequest` — **`AccountType` has no `[StringLength]`** though every other string field does; unbounded input reaches `AccountType.IsCompany` (which `.Trim()`s the whole string). low. Fix: `[StringLength(32)]`.
- `AuthEndpointsTests` — **the "invalid cookie" half of the `GET /api/auth/me` matrix row is untested** (only the no-cookie case). low. Fix: a `[Fact]` sending `Cookie: nexusjob_auth=<garbage>` → `401`.
- `IdentityModule` placeholder conn string + `DbConnectionStrings.ForDataProtection` — **placeholders have no `Timeout=`** unlike the health probe, so `Database.Migrate()` against a packet-dropping `localhost` blocks ~15s before the caught exception (slows `HealthEndpointTests.Unknown_api_route…`; CI refuses fast). low. Fix: `Timeout=3;Command Timeout=3` on both placeholders (one shared const).
- `LoginHandler` `SuccessRehashNeeded` branch — an unguarded `SaveChangesAsync` on the opportunistic-rehash path turns a **correct-credentials login into a `500`** if that write fails transiently. low. Fix: best-effort try/catch around the rehash save; still `SignInAsync` and return `200`.

**defer:** no rate limiting / lockout / throttling on `POST /api/auth/login` (or register), and `deferred-work.md` did not record the omission. Not required by the PRD/epics/ADs for v1, and a real fix is middleware + config + tests. → `deferred-work.md` entry.

**rejected:**
- edge-case-hunter (×2, one filed as a claim) — "`new PostgreSqlBuilder("postgres:18")` has no such constructor, the test project will not compile": **false**. `dotnet build backend/NexusJob.sln` returns 0 errors and `dotnet test` runs `NexusJob.IntegrationTests` 19/19 against a real `postgres:18` container (orchestrator's own run) — the string constructor exists in `Testcontainers.PostgreSql` 4.14.0.
- edge-case-hunter — "whitespace-only `name` passes `[Required(AllowEmptyStrings=false)]` and writes an empty `display_name`": **false**. `RequiredAttribute.IsValid` calls `stringValue.Trim().Length != 0` when `AllowEmptyStrings` is false, so `"   "` is a `400`.
- blind-hunter — "`OnRedirectToLogin`/`OnRedirectToAccessDenied` write a body inside the redirect event, risking a race": low. The 401 path is verified by `Me_without_a_cookie…` (`application/problem+json`, status 401) across 19 green tests; the 403 path is unreachable in 1.3a (no role policy — `RequireAuthorization()` yields 401 for anonymous).
- blind-hunter — "`Id` is `ValueGeneratedOnAdd` not `ValueGeneratedNever`": low. EF's default for a `Guid` key; the app always assigns `Guid.CreateVersion7()` and the migration adds no server default, so the generator never runs — a metadata nicety with no behavioural effect.
- blind-hunter + verification-gap — "no concurrent-duplicate-registration test": low. The `DbUpdateException`/`23505` catch is the standard defensive EF pattern; a real race test is flaky and high-cost for a path the sequential test already proves returns `409`.
- edge-case-hunter — "`AntiforgeryEndpointFilter` only catches `AntiforgeryValidationException`; a key-ring/DB failure → unshaped `500`": low. Only reachable when the database is down, a degraded state the spec's Implementation Notes already acknowledge returns `500` on `/api/auth/csrf`.
- edge-case-hunter — "`GetCsrfTokenHandler` `?? string.Empty` → `200` with an empty token": low. `IAntiforgery.GetAndStoreTokens` returns a non-null `RequestToken` for any valid request; the coalesce is unreachable defence.
- blind-hunter + verification-gap + edge-case-hunter — "`GET /api/auth/csrf` DB-down `500` has no `.ProducesProblem(500)` and no test": low. 1.3b owns the OpenAPI document and can add the metadata with its NSwag work; the behaviour is a DB-down degraded state already acknowledged.
- verification-gap — "`account_type` cookie claim is never asserted (no consumer)": low. `ClaimsPrincipalFactory.ForCompany` demonstrably emits it; `NexusJob.IntegrationTests` cannot reference the module to decrypt the cookie, and story 1.4 is its first consumer and will assert it.
- verification-gap — "the `image` smoke test would not catch a failed startup migration": low. The same `Program.cs` path is covered by `Host_started_with_a_reachable_database_applied_both_migrations…`; the smoke job's `/health` `SELECT 1` is a coarse check by design.
- blind-hunter — "cookie lifetime `7 days` / `SlidingExpiration` is a hardcoded magic literal": low. A reasonable default the spec did not call out; making it configurable is unrequested scope.
- blind-hunter — "brittle/inconsistent health-body assertions (`Contains` vs full equality)": low. Both assertions pass; a test-readability nit.
- blind-hunter — "duplicated `"Authentication is required."` title across `Program.cs` and `GetMeHandler`": low. Two copies in two projects with no shared home; a constant is not worth the coupling. (The duplicated placeholder connection strings are consolidated by the `Timeout=` patch above.)
- blind-hunter — "Code Map names `PostgresFixture`; code ships `IdentityApiFixture`, and new test files aren't enumerated": rejected — the fix edits this build's spec (a non-frozen planning section), and post-implementation Code Map drift is expected.
- blind-hunter — "1.3a and 1.3b share one `sprint-status` key": low. A deliberate, recorded decision (`deferred-work.md` + the `sprint-status.yaml` inline comment); the numeric story-key matcher cannot carry a `1-3a`/`1-3b` split.
- blind-hunter — "`postgres:18` not digest-pinned; no CI image cache": low. Consistent with story 1.1's rejection of digest-pinning base images (Renovate territory); image caching is an unrequested CI optimisation.

## Design Notes

- **Cookie scheme / antiforgery / Data-Protection live in the Host, not `AddIdentityModule`.** They are app-wide infrastructure (one cookie scheme for the whole app, AD-13; Data-Protection keys are Host-owned, AD-22). `AddIdentityModule` registers only Identity's `DbContext`, `PasswordHasher`, and handlers. Identity's handlers *use* the scheme (`SignInAsync`/`SignOutAsync` with `AuthCookie.Scheme`) but do not register it. This keeps "only Identity issues/clears the cookie" (AD-13) true at the behaviour level without the module owning framework wiring.
- **Failure status codes:** duplicate email → `409 Conflict`; bad credentials (unknown email *or* wrong password) → `401 Unauthorized` with one byte-identical generic ProblemDetails; `accountType != company` and DataAnnotations failures → `400`. AD-15 only mandates ProblemDetails on non-2xx; these are the chosen codes, recorded here so review does not re-litigate them.
- **Email normalisation:** trim + `ToLowerInvariant` on register and on login lookup; the unique index is on the stored (already-normalised) value. No `citext`, no functional index.
- **`PasswordHasher<T>` needs a type argument** — use `PasswordHasher<CompanyAccount>`. 1.4 will either add `PasswordHasher<JobSeekerAccount>` or switch both to a shared marker; not this story's problem.
- **Register returns `200` with the account summary** (same shape as `/api/auth/me`), not `201 Created` — the SPA (1.3b) treats register and login identically and immediately renders the signed-in state; a `Location` header points nowhere useful here.
- **Integration tests use a low `PasswordHasher` iteration count** (injected via config in the test factory) so PBKDF2 does not dominate the suite runtime; production still reads ≥ 600000 from env. One test asserts the *default* is ≥ 600000 when config is absent.
- **`GET /api/auth/me` reads `display_name` from the table**, keyed by the `NameIdentifier` Guid — not from a claim — so a future display-name change is reflected without re-issuing the cookie (AD-13: identity comes only from `NameIdentifier`).
- **`NexusJob.IntegrationTests` references only `NexusJob.Host`** (like `NexusJob.Host.Tests`), never a module implementation project — otherwise `ProjectReferenceRules` / `BoundaryRules` (AD-2 rule 3) fail. It reaches Identity's behaviour through HTTP via `WebApplicationFactory<Program>`, not by referencing `NexusJob.Modules.Identity`. The story-1.1 module-registry drift test enumerates `backend/NexusJob.Modules.*` only, so the new test project does not need registering anywhere.
- **Tooling:** the build agent may need `dotnet tool install --global dotnet-ef` (or a local tool manifest) to run `dotnet ef migrations add`. Docker must be running for the Testcontainers suite both in the agent's environment and for local verification.

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings, 0 errors.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass — architecture tests, Host tests, and `NexusJob.IntegrationTests` (Testcontainers spins up `postgres:18`; Docker must be available).
- `dotnet ef migrations list --project backend/NexusJob.Modules.Identity` and `--project backend/NexusJob.Host` -- expected: each lists its initial migration.
- `docker compose up`, then a `curl` register → login → me → logout sequence (with the `GET /api/auth/csrf` token) -- expected: the status codes and cookie behaviour in the I/O matrix; `docker compose logs app` shows the migrations applied on boot and no cookie / token / email / password / hash.

**Manual checks:**
- Inspect `identity.company_account` after a register call: one row, `password_hash` is a long base64 PBKDF2 string, `email` is lower-cased.
- Confirm `data_protection_keys` rows appear in the `public` schema after first cookie issuance and that a Host restart keeps existing cookies valid.
