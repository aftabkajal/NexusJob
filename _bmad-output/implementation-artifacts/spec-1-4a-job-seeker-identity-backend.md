---
title: 'Job Seeker Identity backend: the job_seeker_account space and the /api/auth/* branches'
type: 'feature'
created: '2026-09-07'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'beb4d2446a6b5e4c55fbe3aafce4b4c3da009af1'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.3a shipped only the Company half of Identity: `company_account`, and `/api/auth/{register,login}` that reject any `accountType` other than `"company"`. A job seeker cannot create an account or sign in, so no later story can gate an application on a Job Seeker session.

**Approach:** Add an independent `job_seeker_account` table in the `identity` schema and branch `register` / `login` / `me` on `accountType`, reusing every piece of the cookie / antiforgery / hashing / logging plumbing 1.3a built. This is story **1.4a** (backend + integration tests); re-enabling the Job Seeker option on the Sign up / Log in surface is **1.4b** (see `deferred-work.md`).

## Boundaries & Constraints

**Always:**
- Follow the architecture ADs exactly as 1.3a did: AD-3 (feature-slice folders `Modules/Identity/Features/<VerbNoun>/`, no layering), AD-5 (`IdentityDbContext` maps only Identity's entities, default schema `identity`), AD-7 (Identity's own EF migration history), AD-8 (one write request = one module, one `SaveChanges`), AD-11 (`job_seeker_account`, `email` unique **within that table**, defined independently of `company_account`, no FK crossing between the two), AD-13 (cookie auth, `PasswordHasher` PBKDF2 with `IterationCount` from config ≥ 600000, `HttpOnly; Secure; SameSite=Lax`, claims `NameIdentifier` = account id + `account_type` = `job_seeker`, identity id read from `NameIdentifier` only, antiforgery unchanged, credentials/hashes/cookies never logged), AD-14 (endpoint delegates call slice handler classes — no mediator), AD-15 (RFC 9457 ProblemDetails on every non-2xx; success returns the representation directly).
- The two account spaces are fully independent: the same email may hold one `company_account` and one `job_seeker_account` as unrelated identities. A `job_seeker` register with an email already present in `company_account` (and absent from `job_seeker_account`) **succeeds**. Duplicate checks are scoped to one table.
- Identifiers: `Guid.CreateVersion7()` in app code; DB columns `id`, `email`, `password_hash`, `full_name`, snake_case. Email trimmed + `ToLowerInvariant` via the existing `EmailNormalizer`; the unique index is on the stored normalised value; login looks up by the same normalised value.
- Job Seeker sign-in failure returns the one generic `401` ProblemDetails — byte-identical for "no such email" and "wrong password" (differing only by the per-request `traceId`), and runs the same fixed-cost dummy-hash `VerifyHashedPassword` on the unknown-email path as the Company path (no timing side-channel).
- `TreatWarningsAsErrors` stays on; `dotnet build` and the full `dotnet test` (`NexusJob.ArchitectureTests`, `NexusJob.Host.Tests`, `NexusJob.IntegrationTests`) are green, including the unchanged Company rows. Every new I/O-matrix row is covered by a Testcontainers integration test that runs against real Postgres.
- The Host stays free of Job-Seeker-specific code: only the two `AddIdentityModule` / `MapIdentityModule` calls and the app-wide auth infrastructure. `NexusJob.Modules.Identity.Contracts` still declares no `IIdentityApi` and no DTOs.
- The OpenAPI document does not change shape: `POST /api/auth/register` and `/login` already accept `accountType` in the body and declare `ProducesProblem(400)` / `(409)`. `npm run generate:api` must still leave `git status` clean; `nswag.json` and `shared/api/nexus-api-client.ts` are not touched.

**Never:**
- No frontend change of any kind — no `features/auth`, no `entities/session`, no `RoleToggle`, no shell, no tests under `frontend/` (all 1.4b).
- No third account type, no account-linking or "sign in as" switching between a Company and a Job Seeker identity, no merge of the two tables, no shared base table.
- No change to `POST /api/auth/logout`, `GET /api/auth/csrf`, the cookie scheme, the antiforgery configuration, the Data-Protection wiring, or the startup-migration mechanism — Job Seeker reuses all of it unchanged.
- No new endpoint, no route, no API versioning, no `[ExcludeFromDescription]`, no Swagger UI.
- No full ASP.NET Core Identity framework, no `UserManager`, no FluentValidation, no mediator, no domain-event bus.
- No secret, connection string, or iteration count committed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Register a new Job Seeker | `POST /api/auth/register` `{accountType:"job_seeker", name, email, password}`, valid antiforgery token, email not in `job_seeker_account` | `200` `{ id, accountType:"job_seeker", displayName:name }`; `Set-Cookie` auth cookie (`HttpOnly; Secure; SameSite=Lax`) carrying `NameIdentifier` = new id + `account_type=job_seeker`; exactly one `job_seeker_account` row, `password_hash` a PBKDF2 hash (never the plaintext), single `SaveChanges` in `identity` | N/A |
| Register, email already a Job Seeker | same, email already in `job_seeker_account` (case-insensitively) | `409` RFC 9457 ProblemDetails, generic title; no second row; no cookie | ProblemDetails; does not echo the password |
| Register `job_seeker`, email is a Company but not a Job Seeker | email in `company_account`, absent from `job_seeker_account` | `200`; a `job_seeker_account` row is created; the `company_account` row is untouched | N/A |
| Register, `accountType` unrecognised | `{accountType:"admin", …}` or empty/whitespace | `400` ProblemDetails ("Unsupported account type."); no row in `company_account` or `job_seeker_account` | ProblemDetails |
| Register `job_seeker`, invalid body | missing/invalid `email`, `password` < 8 chars, empty/whitespace `name` | `400` validation ProblemDetails naming the invalid fields; no row | ProblemDetails (validation filter) |
| Login, correct Job Seeker credentials | `POST /api/auth/login` `{accountType:"job_seeker", email, password}`, antiforgery token, matching `job_seeker_account` row | `200` `{ id, accountType:"job_seeker", displayName }`; same cookie shape as register (`account_type=job_seeker`) | N/A |
| Login, wrong password or unknown Job Seeker email | matching-shape body, no `job_seeker_account` row or hash mismatch | one generic `401` ProblemDetails — identical body for both cases; no cookie; a fixed-cost dummy `VerifyHashedPassword` runs on the missing-account path | ProblemDetails, no field-level detail |
| Login, `job_seeker` email exists only as a Company | email in `company_account`, not in `job_seeker_account` | the same generic `401` (the `job_seeker` space has no such account) | ProblemDetails |
| Login, `accountType` unrecognised | `{accountType:"admin", …}` | the same generic `401` (login must not reveal which part failed) | ProblemDetails |
| `GET /api/auth/me`, signed-in Job Seeker | valid auth cookie with `account_type=job_seeker` | `200` `{ id, accountType:"job_seeker", displayName }`; `displayName` read fresh from `job_seeker_account.full_name`, id from `NameIdentifier` only | N/A |
| `GET /api/auth/me`, signed-in Company | valid auth cookie with `account_type=company` | unchanged from 1.3a — `200` `{ …, accountType:"company", … }` from `company_account` | N/A |
| `GET /api/auth/me`, `account_type` claim missing/unknown | cookie without a recognised `account_type` claim | `401` ProblemDetails | ProblemDetails |
| Migration applied | `dotnet ef database update` (or the Host startup migration) on a schema that has only `company_account` | `identity.job_seeker_account` exists with `id` (uuid PK), `email`, `password_hash`, `full_name` (all `not null`) and a unique index on `email`; `company_account` unchanged; history row in `identity.__EFMigrationsHistory` | startup-migration failure caught + logged (existing behavior, unchanged) |
| Structured logs during any of the above | any Job Seeker `/api/auth/*` request | one JSON log line per request (method/path/status/duration only); no `Set-Cookie`, `X-CSRF-TOKEN`, email, password, or hash value anywhere | N/A |

</frozen-after-approval>

## Resolved Decisions

- **`register` / `login` / `me` branch on `accountType` inside the existing slice handlers**, rather than adding parallel `RegisterJobSeeker` / `LoginJobSeeker` slices. `POST /api/auth/register` and `/login` are single endpoints whose body already carries `accountType` (AD-14: one endpoint → one handler); a branch keeps the antiforgery filter, validation filter, cookie sign-in, generic-`401`, dummy-hash, and log redaction in one place. The Job Seeker entity, its `DbContext` mapping, and its migration are the only genuinely new units.
- **`GET /api/auth/me` picks the table from the `account_type` cookie claim** (`company` → `company_account`, `job_seeker` → `job_seeker_account`); the account **id** still comes only from `NameIdentifier` (AD-13). An absent or unrecognised `account_type` claim → `401`.
- **`PasswordHasher<JobSeekerAccount>` is registered alongside `PasswordHasher<CompanyAccount>`**, both reading the one `PasswordHasherOptions.IterationCount` from config. The two handlers inject both. (`PasswordHasher<T>` uses `T` only as a generic tag, but a distinct registration keeps the DI graph and the call sites honest.)
- **The `400` for a bad `accountType` changes wording.** 1.3a returned `400` with detail "Job seeker registration is not available yet." for `accountType != "company"`. After 1.4a a `job_seeker` register succeeds; an *unrecognised* `accountType` returns `400` `Results.Problem(title: "Unsupported account type.", statusCode: 400)` with no per-field `errors`. Login treats an unrecognised `accountType` as just another credential failure → the shared generic `401`.

## Code Map

**New**
- `backend/NexusJob.Modules.Identity/Persistence/JobSeekerAccount.cs` -- NEW. `internal sealed class` mirroring `CompanyAccount.cs`: `Guid Id`, `required string Email`, `required string PasswordHash`, `required string FullName`. No relationship to `CompanyAccount`.
- `backend/NexusJob.Modules.Identity/Persistence/Migrations/<timestamp>_AddJobSeekerAccount.cs` (+ `.Designer.cs`) -- NEW, produced by `dotnet ef migrations add AddJobSeekerAccount --project backend/NexusJob.Modules.Identity`. Creates `identity.job_seeker_account` (`id` uuid PK, `email` text not null, `password_hash` text not null, `full_name` text not null) + a unique index on `email`. Must **not** alter `company_account`.

**Modify**
- `backend/NexusJob.Modules.Identity/Persistence/IdentityDbContext.cs` -- add `internal DbSet<JobSeekerAccount> JobSeekerAccounts => Set<JobSeekerAccount>();`; in `OnModelCreating` add a `modelBuilder.Entity<JobSeekerAccount>(...)` block exactly parallel to the `CompanyAccount` one — `ToTable("job_seeker_account")`, snake_case column names (`id`, `email`, `password_hash`, `full_name`), `HasKey(e => e.Id)`, `HasIndex(e => e.Email).IsUnique()`. Default schema `identity` already set.
- `backend/NexusJob.Modules.Identity/Persistence/Migrations/IdentityDbContextModelSnapshot.cs` -- regenerated by the `ef migrations add` command; commit as-is (no hand edits).
- `backend/NexusJob.Modules.Identity/Auth/AccountType.cs` -- add `public const string JobSeeker = "job_seeker";` and `public static bool IsJobSeeker(string? value)` (trim + `StringComparison.OrdinalIgnoreCase`, mirroring `IsCompany`). Consider a shared `Classify(string?) : "company" | "job_seeker" | null` the three handlers use.
- `backend/NexusJob.Modules.Identity/Auth/ClaimsPrincipalFactory.cs` -- add `public static ClaimsPrincipal ForJobSeeker(Guid accountId)` identical to `ForCompany` but emitting `AccountType.JobSeeker` for the `account_type` claim.
- `backend/NexusJob.Modules.Identity/Features/Register/RegisterHandler.cs` -- constructor also takes `IPasswordHasher<JobSeekerAccount>`. Branch: `AccountType.IsCompany` → the current path, unchanged; `AccountType.IsJobSeeker` → `var email = EmailNormalizer.Normalize(request.Email)`; `if (await db.JobSeekerAccounts.AnyAsync(a => a.Email == email, ct)) return DuplicateEmail();` (reuse the existing `409` helper / same generic title); else build `new JobSeekerAccount { Id = Guid.CreateVersion7(), Email = email, FullName = request.Name.Trim(), PasswordHash = string.Empty }`, `PasswordHash = jobSeekerHasher.HashPassword(account, request.Password)`, `db.JobSeekerAccounts.Add(account)`, one `SaveChangesAsync` wrapped in the existing `DbUpdateException` / `PostgresErrorCodes.UniqueViolation` catch → `DuplicateEmail()`, `await httpContext.SignInAsync(AuthCookie.Scheme, ClaimsPrincipalFactory.ForJobSeeker(account.Id))`, `return Results.Ok(new AuthAccountResponse(account.Id.ToString(), AccountType.JobSeeker, account.FullName));`. Neither type → `Results.Problem(title: "Unsupported account type.", statusCode: StatusCodes.Status400BadRequest)`.
- `backend/NexusJob.Modules.Identity/Features/Login/LoginHandler.cs` -- constructor also takes `IPasswordHasher<JobSeekerAccount>`. Branch on `IsCompany` / `IsJobSeeker`; the Job Seeker path mirrors the Company path against `db.JobSeekerAccounts.SingleOrDefaultAsync(a => a.Email == email, ct)` — the same `DummyPasswordHash` fixed-cost verify when the row is null, the same `PasswordVerificationResult.Failed` → `InvalidCredentials()`, the same best-effort `SuccessRehashNeeded` re-hash in a `try { … } catch (DbUpdateException) { }`, `SignInAsync(ForJobSeeker(id))` on success, `AuthAccountResponse(id, AccountType.JobSeeker, fullName)`. An `accountType` that is neither → `InvalidCredentials()` (the shared generic `401`).
- `backend/NexusJob.Modules.Identity/Features/GetMe/GetMeHandler.cs` -- after parsing `accountId` from `NameIdentifier`, read `httpContext.User.FindFirstValue(AccountType.ClaimType)`: `AccountType.Company` → the current `company_account` projection; `AccountType.JobSeeker` → `db.JobSeekerAccounts.Where(a => a.Id == accountId).Select(a => new AuthAccountResponse(a.Id.ToString(), AccountType.JobSeeker, a.FullName)).SingleOrDefaultAsync(ct)`; anything else → `Unauthorized()`. `summary is null` → `Unauthorized()` on both branches.
- `backend/NexusJob.Modules.Identity/IdentityModule.cs` -- in `AddIdentityModule`, next to the Company hasher: `services.AddScoped<IPasswordHasher<JobSeekerAccount>, PasswordHasher<JobSeekerAccount>>();`. The existing `services.Configure<PasswordHasherOptions>(o => o.IterationCount = iterationCount)` already applies to every `PasswordHasher<T>`. `MapIdentityModule` unchanged — the `Auth_Register` / `Auth_Login` metadata (`Accepts<RegisterRequest>`, `ProducesProblem 400`, `ProducesProblem 409`) already covers the Job Seeker outcomes.
- `backend/NexusJob.Modules.Identity/Features/Register/RegisterRequest.cs`, `Login/LoginRequest.cs` -- no change. `AccountType` is `[Required, StringLength(32)]`; `Name` is `[Required, StringLength(200, MinimumLength = 1)]` which fits `full_name`.

**Tests**
- `backend/NexusJob.IntegrationTests/TestSupport.cs` -- add to `IdentityDatabase`: `CountJobSeekerAccountsAsync(string email)` and `GetJobSeekerAccountAsync(string email)` returning `(string Email, string PasswordHash, string FullName)?`, querying `identity.job_seeker_account` (columns `email, password_hash, full_name`), exactly parallel to the Company helpers. `AuthApiClient.RegisterAsync` / `LoginAsync` already accept an `accountType` argument — no change.
- `backend/NexusJob.IntegrationTests/AuthEndpointsTests.cs` -- **change** `Register_with_a_non_company_account_type_returns_400_problem_details_and_writes_no_row` (it registers `accountType:"job_seeker"` and asserts `400`): repoint at an unrecognised type (`"admin"`), asserting `400` + `AssertProblemDetailsAsync` + zero rows in **both** `company_account` and `job_seeker_account`. **Add** `[Fact]`s: Job Seeker register happy path (`200`, `accountType == "job_seeker"`, one hashed `job_seeker_account` row, cookie flags, single `Set-Cookie`); Job Seeker duplicate (case-insensitive) → `409` + no 2nd row + no cookie; **independence** — register the same email as a Company and as a Job Seeker, both `200`, one row in each table; Job Seeker login match → `200` + `account_type=job_seeker` cookie; Job Seeker login unknown-email and wrong-password → one generic `401` with the same body (assert title/status/no-detail, not raw bytes); `GET /api/auth/me` for a signed-in Job Seeker → `{accountType:"job_seeker", displayName == full_name}`; a `job_seeker_account` table/columns/unique-index assertion (extend `IdentityDatabase` with a small schema probe, or assert the `dotnet ef migrations list` output in a Host-tests-style check). The existing `No_cookie_token_email_password_or_hash_value_appears_in_the_structured_logs` should be extended (or paralleled) to exercise a Job Seeker register+login.

**Reference — do not change**
- `backend/NexusJob.Modules.Identity/Auth/{AntiforgeryEndpointFilter,DataAnnotationsValidationFilter,EmailNormalizer,AuthCookie}.cs`; `Features/{Logout,GetCsrfToken}/*`; `Features/Register/AuthAccountResponse.cs`; `backend/NexusJob.Host/Program.cs`; `backend/NexusJob.Modules.Identity/Persistence/CompanyAccount.cs`.
- `backend/NexusJob.ArchitectureTests/*` -- read-only; the new module code must keep the AD-2 boundary gates green (no new project reference, no raw cross-schema SQL).
- `_bmad-output/implementation-artifacts/spec-1-3a-identity-backend-and-auth-endpoints.md` -- continuity: the shared `AuthApiClient` / `IdentityDatabase` test helpers, the generic-`401` + fixed-cost dummy-hash pattern, the `23505` unique-violation race catch, the `200`-not-`201` register response shape, and the cookie-flag assertions the new tests mirror.

## Tasks & Acceptance

**Execution:**
- [x] `backend/NexusJob.Modules.Identity/Persistence/JobSeekerAccount.cs` + `IdentityDbContext.cs` -- entity + `job_seeker_account` mapping (snake_case columns, unique index on `email`), independent of `CompanyAccount`.
- [x] `backend/NexusJob.Modules.Identity/Persistence/Migrations/*` -- `20260907094516_AddJobSeekerAccount`; creates only `identity.job_seeker_account` + its unique `email` index, `company_account` untouched; model snapshot regenerated (additive only).
- [x] `backend/NexusJob.Modules.Identity/Auth/AccountType.cs` + `ClaimsPrincipalFactory.cs` -- `JobSeeker` constant, `IsJobSeeker`, `Classify(...)`; `ForJobSeeker(Guid)` via a shared private `For(...)`.
- [x] `backend/NexusJob.Modules.Identity/Features/Register/RegisterHandler.cs` -- `Classify` switch; `RegisterJobSeekerAsync` (table-scoped duplicate → `409`, `Guid.CreateVersion7()`, hash, `SaveChanges` + `23505` race catch → `409`, `ForJobSeeker` sign-in, `200`); unrecognised type → `400` "Unsupported account type.".
- [x] `backend/NexusJob.Modules.Identity/Features/Login/LoginHandler.cs` -- `Classify` switch; `LoginJobSeekerAsync` mirrors the Company path (fixed-cost dummy verify, best-effort re-hash, `ForJobSeeker` sign-in); any failure or unrecognised type → the shared generic `401`.
- [x] `backend/NexusJob.Modules.Identity/Features/GetMe/GetMeHandler.cs` -- `account_type` claim selects the table (`company` / `job_seeker`); Job Seeker returns `full_name`; unknown/absent claim or no row → `401`; id still from `NameIdentifier`.
- [x] `backend/NexusJob.Modules.Identity/IdentityModule.cs` -- registers `IPasswordHasher<JobSeekerAccount>`.
- [x] `backend/NexusJob.IntegrationTests/TestSupport.cs` -- `CountJobSeekerAccountsAsync` / `GetJobSeekerAccountAsync` / `GetJobSeekerAccountSchemaAsync` (+ `JobSeekerAccountSchema`).
- [x] `backend/NexusJob.IntegrationTests/AuthEndpointsTests.cs` -- repointed the non-company test to `accountType:"admin"`; added `[Fact]`s for register happy / duplicate (case-insensitive) / independence (×2) / invalid job_seeker body, login match / wrong-pw-vs-unknown-email identical `401` / company-only-email `401` / unrecognised-type `401`, Job Seeker `/me`, migration shape, and Job Seeker log redaction.

**Acceptance Criteria:**
- Given a clean `identity` schema containing only `company_account`, when the `AddJobSeekerAccount` migration is applied, then `identity.job_seeker_account` exists with `id` (uuid PK), `email`, `password_hash`, `full_name` and a unique index on `email`, `company_account` is unchanged, and the history table is `identity.__EFMigrationsHistory`.
- Given `dotnet build backend/NexusJob.sln` and `dotnet test backend/NexusJob.sln`, when they run, then the build is warning-free and every suite passes — the ArchUnitNET boundary gates, the unchanged Company `HealthEndpointTests` / `AuthEndpointsTests` rows, and the new Job Seeker rows.
- Given `AddIdentityModule` / `MapIdentityModule`, when the Host wires Identity, then the Host contains no Job-Seeker-specific code beyond the two module calls, and `NexusJob.Modules.Identity.Contracts` still declares no `IIdentityApi` and no DTOs.
- Given a Host build, when `cd frontend && npm run generate:api` runs, then `git status --porcelain src/shared/api` is empty (the OpenAPI document shape is unchanged) and no file under `frontend/` is modified by this story.
- Given any Job Seeker `/api/auth/*` request in the new tests, when it completes, then no `Set-Cookie`, `X-CSRF-TOKEN`, email, password, or password-hash string appears in the structured logs.

## Implementation Notes

- **Two layers reject a bad `accountType`, not one.** The frozen I/O matrix groups "`admin`" and "empty/whitespace" under one row with the title `"Unsupported account type."`. In the shipped stack they take different (both correct) paths: a non-empty unrecognised value (`"admin"`) reaches `RegisterHandler`'s `_ =>` arm and gets `400` `"Unsupported account type."` (no `detail`, no `errors`); a whitespace-only value trips `[Required(AllowEmptyStrings = false)]` on `RegisterRequest.AccountType` in the validation filter *first* and gets the generic `400` validation ProblemDetails (`"One or more validation errors occurred."` with an `errors` map). Both yield `400` + no row in either table — the acceptance-criteria behaviour. `RegisterRequest.cs` was left unchanged (spec Code Map), so the whitespace test asserts the outcome (`400`, no rows) without pinning the title; the `"admin"` test pins the re-worded title / `detail` / `errors` contract.
- **All three `/api/auth/*` handlers now route the account type through `AccountType.Classify`** (review pass 1). `GetMeHandler` originally matched the raw `account_type` claim string in a case-sensitive `switch`; it now calls `Classify` like `RegisterHandler` / `LoginHandler`, so the behaviour is consistent and the `Classify` doc comment is accurate. The reachable path is unchanged (`ClaimsPrincipalFactory` always writes the canonical value into a Data-Protection-sealed cookie).

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-07) — blind-hunter, edge-case-hunter, verification-gap

No intent_gap or bad_spec — no loopback. 3 `patch` groups, 1 `defer`, the rest rejected. Local re-verification (`dotnet build` 0W/0E, `dotnet test` = ArchitectureTests 22, Host.Tests 3, IntegrationTests 35) was green before the pass.

**patch:**
- `AuthEndpointsTests.Register_with_an_unrecognised_account_type_…` (verification-gap main, pre-verified; also blind) — the test asserts only `400` + `application/problem+json` + zero rows; it never reads the body, so the deliberately re-worded contract (title `"Unsupported account type."`, no `detail`, no `errors` — spec §Resolved Decisions) is unpinned. A regression restoring 1.3a's now-misleading `detail: "Job seeker registration is not available yet."` would ship green. medium. Fix: deserialize with `ProblemBody`, assert `Title == "Unsupported account type."`, `Detail == null`, `Errors == null`; add a whitespace `accountType` (`"   "`) sub-case (the matrix names "empty/whitespace").
- `GetMeHandler.cs` (blind, edge) — the `account_type` `switch` matches the raw claim string case-sensitively / untrimmed, unlike `AccountType.Classify` used by `RegisterHandler` / `LoginHandler`; and `Classify`'s doc says "the three `/api/auth/*` handlers branch on this" while only two do. low (unreachable — `ClaimsPrincipalFactory` always writes the canonical value into a Data-Protection-sealed cookie — but an inconsistency and an untrue comment). Fix: resolve the claim through `AccountType.Classify(...)` before the switch, making all three handlers consistent and the comment accurate.
- `AuthEndpointsTests` job-seeker rows — hardening + symmetry (blind ×9): `The_same_email_can_register_as_both_…` never signs in and calls `/me` (the claim→table routing is the core new behaviour) and never asserts the two rows' `Id`s differ; there is no symmetric test for registering a **Company** when the email is a Job-Seeker only; the trimmed / case-insensitive `IsJobSeeker` / `Classify` path has no test; `AddJobSeekerAccount_migration_…` captures every column's `DataType` but only asserts `id` is `uuid`, and its comment overstates the `company_account`-undisturbed check; `Login_with_correct_job_seeker_credentials_…` drops the `secure` cookie-flag assertion the register test makes; `Register_new_job_seeker_…` never asserts the plaintext password is absent from the response body; `Me_for_a_signed_in_job_seeker_…` reads the register body without first asserting `reg.StatusCode == OK`; `Register_job_seeker_with_an_invalid_body_…` asserts no `job_seeker` row but not no `company` row; `Login_with_an_unrecognised_account_type_…` omits `Assert.Null(problem.Errors)`. low. Fix: apply each small assertion; add the symmetric "register Company when the email is a Job Seeker" test; add a trimmed / mixed-case `accountType` register case.

**defer:**
- The `SuccessRehashNeeded` opportunistic re-hash in `LoginCompanyAsync` and (new) `LoginJobSeekerAsync` catches only `DbUpdateException`; a raw `NpgsqlException` / timeout / connection drop during that best-effort `SaveChangesAsync` turns a verified-correct login into a `500`, contradicting the block's own comment (edge-case-hunter). Pre-existing in the Company path; this story mirrors it into the Job Seeker path. The narrow `catch (DbUpdateException)` is spec-directed in the Code Map (parity with 1.3a). → `deferred-work.md`: widen both re-hash catches consistently to swallow any non-`OperationCanceledException` on the opportunistic re-hash.

**rejected:**
- blind — no Job Seeker logout test: `POST /api/auth/logout` is unchanged and account-type-agnostic (`SignOutAsync`); the shipped Company logout test covers the mechanism; not a row in the 1.4a matrix. low.
- blind — timing: an unrecognised `accountType` on login returns `401` before any PBKDF2 work, so it is measurably faster than "recognised type, wrong password": the valid `accountType` set (`company`, `job_seeker`) is public API contract, so the distinction leaks nothing an attacker cannot read in the docs; account *enumeration* (which email exists) is still mitigated by the fixed-cost dummy verify. false / low.
- blind — no defence against the same `Guid` as `id` in both tables: `Guid.CreateVersion7()` collision is ~0, and even then the `account_type` claim (authoritative, set at sign-in, Data-Protection-sealed) routes `/me` to the correct account for that session. false.
- edge-case-hunter — a non-`23505` `DbUpdateException` in `RegisterJobSeekerAsync` escapes unshaped: identical to the shipped `RegisterCompanyAsync` `catch … when` filter; `AddProblemDetails()` turns an uncaught exception into a `500` ProblemDetails; a broad `catch` would mask real bugs. low, pre-existing pattern.
- verification-gap (other) — `GET /api/auth/me` with a valid cookie but a missing / non-canonical `account_type` claim → `401` has no test: not reachable through the public API (`ClaimsPrincipalFactory` always stamps the canonical value; the cookie is Data-Protection-sealed), so a test needs a test-only auth handler; the `_ => null → 401` arm is a correct guard on an unreachable state (and the `Classify` patch above makes it stricter still). low.
- verification-gap (other) — the `SuccessRehashNeeded` path is untested on both the Company and the new Job Seeker branch: pre-existing untested state, not worsened beyond the mirror; folded into the `defer` above.

## Design Notes

- **Independence is the load-bearing behavior.** The "same email registers as both a Company and a Job Seeker" AC proves the two tables and their unique indexes are genuinely separate. Keep every duplicate/lookup query scoped to a single `DbSet` — never a union or a cross-table `OR`.
- **`GetMeHandler` and the `account_type` claim.** AD-13's "identity from `NameIdentifier` only" governs *which account* — the id. The `account_type` claim is legitimate dispatch metadata that `ClaimsPrincipalFactory` sets at sign-in; using it to choose the table avoids probing both tables with one id (Guid v7 is not guaranteed unique across two tables). A tampered or missing claim simply yields `401`.
- **`PasswordHasher<T>` genericity.** The type parameter is a tag; `HashPassword` does not read the instance. Two registrations (`<CompanyAccount>`, `<JobSeekerAccount>`) both pick up the configured `IterationCount`. If injecting two `IPasswordHasher<T>` into one handler feels heavy, a single `IPasswordHasher<object>` (or an internal marker) is acceptable — but do not change the Company path's existing `IPasswordHasher<CompanyAccount>` dependency.
- **The three failure codes, recorded so review does not re-litigate:** Job Seeker duplicate email → `409 Conflict` (same generic title as the Company path); bad credentials (unknown email, wrong password, or an unrecognised `accountType` on login) → one byte-identical generic `401`; DataAnnotations failure or an unrecognised `accountType` on register → `400`.
- **Test-key note.** 1.4a and 1.4b share the `1-4-job-seeker-registration-and-sign-in` sprint-status key (as 1.3a / 1.3b-i / 1.3b-ii shared `1-3-…`); the numeric story-key matcher cannot carry an `a`/`b` suffix. Recorded here + in `deferred-work.md`.

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings, 0 errors.
- `dotnet ef migrations list --project backend/NexusJob.Modules.Identity` -- expected: `20260906173942_InitialIdentity` then `<timestamp>_AddJobSeekerAccount`.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass, including the new Job Seeker rows in `AuthEndpointsTests` (Testcontainers spins up `postgres:18`; Docker must be running).
- `docker compose up`, then a `curl` sequence: `GET /api/auth/csrf` → `POST /api/auth/register {accountType:"job_seeker",…}` → `GET /api/auth/me` → `POST /api/auth/login {accountType:"job_seeker",…}` -- expected: the status codes and `account_type=job_seeker` cookie from the I/O matrix; `docker compose logs app` shows no cookie / token / email / password / hash.
- `cd frontend && npm run generate:api && git status --porcelain src/shared/api` -- expected: empty.

**Manual checks:**
- After a Job Seeker register, inspect `identity.job_seeker_account`: one row, `full_name` set, `password_hash` a long base64 PBKDF2 blob, `email` lower-cased. Register the *same* email as a Company too and confirm both rows exist and neither is modified by the other call.
- Decode the auth cookie's claims (or assert via a test): `account_type` is `job_seeker` for a Job Seeker session and `company` for a Company session.
