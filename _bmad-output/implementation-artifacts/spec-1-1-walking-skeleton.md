---
title: 'Walking skeleton with a CI-enforced module boundary gate'
type: 'feature'
created: '2026-09-06'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '13b791a59c547cbeb7b6ed00d6dd639f2dadeca1'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NexusJob has planning artifacts but no code. Epic 1's central claim — enforced module boundaries in a modular monolith (SM-2) — must be proven and continuously enforced from the first commit, before any feature story depends on the structure.

**Approach:** Stand up the full delivery substrate justified by (but not yet implementing) FR-1/FR-2: the .NET 10 modular-monolith solution (Host + three bounded-context modules + per-module Contracts + ArchitectureTests), a Vite/React 19 frontend with Feature-Sliced Design layer folders, build-breaking boundary gates on both sides (ArchUnitNET + `eslint-plugin-boundaries`), a `/health` operability floor, same-origin container hosting, local Docker Compose, and a GitHub Actions pipeline. Modules are wiring-only stubs; persistence, auth, the design system, and features arrive in stories 1.2–1.4.

## Boundaries & Constraints

**Always:**
- Backend projects exactly: `NexusJob.Host`, `NexusJob.Modules.{Identity,JobPostings,Applications}`, `NexusJob.Modules.{Identity,JobPostings,Applications}.Contracts`, `NexusJob.ArchitectureTests`. `NexusJob.Host` is the ONLY project referencing a module implementation; a module implementation references only other modules' `.Contracts`; a `.Contracts` project references no implementation.
- Each module exposes `Add{Context}Module(IServiceCollection, IConfiguration)` and `Map{Context}Module(IEndpointRouteBuilder)`; the Host calls all three pairs and does nothing else module-specific.
- ArchUnitNET tests run under `dotnet test` and fail the build on any of the four AD-2 violations, each with a message naming the violated rule. The test project loads module assemblies dynamically (not via project references) so the "only Host references an implementation" rule holds for it too.
- Frontend is Vite 8 + React 19 + TypeScript with FSD layer folders `app/ pages/ widgets/ features/ entities/ shared/`; `eslint-plugin-boundaries` encodes the downward-only import order `app → pages → widgets → features → entities → shared`, at severity `error`, so `npm run lint` exits non-zero on a violation.
- The Host serves the built SPA as static files and `/api/*` + `/health` from one origin. No `AddCors`/`UseCors`, no proxy, anywhere.
- `GET /health` returns `200` with a JSON body confirming process liveness and a successful `SELECT 1` against Postgres; `503` when the probe fails.
- Logs are structured JSON on stdout. No credential, password hash, or cookie value in any log line. All config/secrets (the DB connection string) come from environment / .NET configuration; nothing secret committed.
- `docker compose up` runs `app` + `postgres` (PostgreSQL 18); the app image is a single container serving API + SPA.
- CI (GitHub Actions) on push/PR: build backend, run architecture tests, run frontend lint + build, build the container image; any step failing fails the pipeline.
- Application code lives in `backend/` and `frontend/` directories at the repository root — the repo root is the project container (the spine's `nexusjob/`). `docker-compose.yml`, `Dockerfile`, `.dockerignore`, `.gitignore`, `README.md`, and `.github/` also sit at the repo root.
- For anything not spelled out here, follow the spine's Consistency Conventions (`Guid` v7 ids, `snake_case` DB identifiers, RFC 9457 ProblemDetails, lowercase-plural REST paths).

**Never:**
- No EF Core `DbContext`, entities, or migrations — the health probe uses a bare `NpgsqlConnection`; persistence lands with Identity in story 1.3.
- No design tokens, no application shell, no auth/cookies/CSRF, no OpenAPI→TypeScript client, no Testcontainers integration tests, no end-to-end core-loop test — later stories own these.
- No feature endpoints or business logic in any module; `Add`/`Map` stubs only. No interfaces in the `.Contracts` projects yet (their DTO surface is designed in stories 2.2 and 3.4).
- No mediator library, no Domain/Application/Infrastructure layering inside modules, no CORS/BFF/YARP.
- No container-registry push and no deploy step — CI builds the image and stops (no hosting platform chosen).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Health, DB up | `GET /health`, Postgres reachable | `200`, JSON `{ "status": "healthy", "database": "ok" }` | N/A |
| Health, DB down | `GET /health`, Postgres unreachable | `503`, JSON `{ "status": "unhealthy", "database": "unreachable" }` | probe exception caught, mapped to `503`, not leaked |
| Arch test, clean tree | `dotnet test` on unmodified solution | all boundary tests pass | N/A |
| Arch test, module→module impl ref | Identity impl references `NexusJob.Modules.JobPostings` | `dotnet test` fails; message names the "no implementation depends on another module's non-Contracts assembly" rule | N/A |
| Arch test, Contracts→impl ref | `Identity.Contracts` references `NexusJob.Modules.Identity` | `dotnet test` fails; message names the "Contracts must not depend on an implementation" rule | N/A |
| Arch test, non-Host→impl ref | any project but Host references a module impl | `dotnet test` fails; message names the "only Host references an implementation" rule | N/A |
| Arch test, cross-schema raw SQL | a `FromSqlRaw`/`ExecuteSql*` call whose literal SQL names a schema other than the caller's | `dotnet test` fails (source scan); message names the cross-schema raw-SQL rule | passes vacuously today (no raw SQL in tree); a fixture string exercises the known-bad path |
| FSD lint, downward import | `features/x` imports from `entities/y` | `npm run lint` exits `0` | N/A |
| FSD lint, upward import | `entities/x` imports from `features/y` | `npm run lint` exits non-zero, names the boundaries rule | N/A |
| Same-origin serve | `docker compose up`; request a SPA route and `/api`/`/health` from one origin | non-API routes return the SPA `index.html`; `/health` responds; no CORS headers or config present | N/A |
| Request logging | any handled request | one structured JSON line on stdout with no `Set-Cookie`, `Authorization`, password, or hash value | N/A |

</frozen-after-approval>

## Code Map

- Greenfield: no application code — no `.sln`, `package.json`, `Dockerfile`, or `.gitignore` exist. `_bmad/`, `_bmad-output/`, `.claude/` are the only top-level directories.
- Toolchain confirmed present: .NET SDK `10.0.302`, Node `v24.18.0` / npm `12.0.2`, Docker `29.6.1` + Compose `v5.2.0`. No git remote (the CI file is authored per this spec; its first real run awaits a GitHub remote).
- `_bmad-output/implementation-artifacts/epic-1-context.md` — Epic 1 scope, conventions, and the cross-story dependency note that this story is the foundation for every later story.
- `_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md` — source of truth for conventions: AD-1/AD-2/AD-3/AD-10 (module structure + gate), AD-12 (same-origin), AD-14 (no mediator), AD-22 (operability floor); the "Stack" table (versions), "Source tree" (project layout), "Consistency Conventions" table.
- `_bmad-output/planning-artifacts/epics.md` — Story 1.1 acceptance criteria (the Given/When/Then this spec's AC mirrors).
- Not in scope to read: `DESIGN.md`, `EXPERIENCE.md`, `prd.md` — story 1.1 touches no visual system or feature behavior.

## Tasks & Acceptance

**Execution:**

_Backend solution_
- [x] `backend/NexusJob.sln` -- create solution referencing all eight projects -- single build entry point for `dotnet build`/`dotnet test` and CI.
- [x] `backend/Directory.Packages.props` -- central package management; pin ArchUnitNET, Npgsql, xUnit, and the test SDK versions -- consistent versions across projects (spine: "the code owns these" after cold-start).
- [x] `backend/Directory.Build.props` -- `net10.0`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true` -- uniform project settings.
- [x] `backend/NexusJob.Host/NexusJob.Host.csproj` + `Program.cs` -- Minimal-API host: `AddJsonConsole` logging; read `ConnectionStrings:Postgres` from configuration; call each `Add{Context}Module`; map `/health` (liveness + `SELECT 1` via a bare `NpgsqlConnection`, with a writer emitting the exact 200/503 JSON from the I/O matrix); call each `Map{Context}Module`; `UseDefaultFiles` + static files from `wwwroot`; `MapFallbackToFile("index.html")` after the API routes; no CORS -- composition root and same-origin server (AD-10, AD-12, AD-22).
- [x] `backend/NexusJob.Modules.Identity/NexusJob.Modules.Identity.csproj` + `IdentityModule.cs` -- project + `AddIdentityModule`/`MapIdentityModule` no-op stubs + `public sealed class IdentityModuleAssembly;` marker -- module wiring pattern in place; the marker lets ArchUnitNET load the assembly.
- [x] `backend/NexusJob.Modules.Identity.Contracts/NexusJob.Modules.Identity.Contracts.csproj` + `IdentityContractsAssembly.cs` marker -- empty public-surface project -- boundary target for the arch rules.
- [x] `backend/NexusJob.Modules.JobPostings/NexusJob.Modules.JobPostings.csproj` + `JobPostingsModule.cs` + marker -- as Identity.
- [x] `backend/NexusJob.Modules.JobPostings.Contracts/NexusJob.Modules.JobPostings.Contracts.csproj` + marker -- as Identity.Contracts.
- [x] `backend/NexusJob.Modules.Applications/NexusJob.Modules.Applications.csproj` + `ApplicationsModule.cs` + marker -- as Identity.
- [x] `backend/NexusJob.Modules.Applications.Contracts/NexusJob.Modules.Applications.Contracts.csproj` + marker -- as Identity.Contracts.
- [x] `backend/NexusJob.ArchitectureTests/NexusJob.ArchitectureTests.csproj` -- xUnit + `ArchUnitNET.xUnit`; NO project references to any module -- keeps the test project off the "references an implementation" list.
- [x] `backend/NexusJob.ArchitectureTests/BoundaryRules.cs` -- load the six module assemblies + Host from the build-output path via `Assembly.LoadFrom`; assert AD-2 rules 1–3 as ArchUnitNET dependency rules, each with an explicit `Because(...)` / failure reason -- the SM-2 gate.
- [x] `backend/NexusJob.ArchitectureTests/RawSqlSchemaScan.cs` (+ inline fixture) -- Roslyn or regex scan of `backend/NexusJob.Modules.*/**/*.cs` for `FromSqlRaw`/`ExecuteSql(Raw|Interpolated)` whose literal SQL names a schema other than the caller module's own; fail with the rule name; a fixture string exercises the known-bad path -- AD-2 rule 4 (AD-5 backstop).

_Frontend_
- [x] `frontend/` -- scaffold from the Vite React-TS template: `package.json`, `vite.config.ts`, `tsconfig*.json`, `index.html`, `src/main.tsx`, `src/App.tsx` (minimal placeholder; the real shell is story 1.2) -- the SPA build the Host serves.
- [x] `frontend/src/{app,pages,widgets,features,entities,shared}/` -- FSD layer folders, each with an `index.ts` barrel or `.gitkeep` -- required structure (AD-16).
- [x] `frontend/eslint.config.js` -- ESLint 9 flat config with `eslint-plugin-boundaries`: `settings["boundaries/elements"]` maps each folder to a layer by path glob; `boundaries/element-types` with `default: "disallow"` and one `allow` list per layer (layers below it, plus `shared`); rule severity `"error"` -- build-breaking FSD gate.
- [x] `frontend/package.json` scripts -- `"lint": "eslint ."`, `"build": "tsc -b && vite build"` -- CI entry points.

_Infra_
- [x] `Dockerfile` -- stage 1 `node:24` builds `frontend/` → `dist/`; stage 2 `dotnet/sdk:10.0` runs `dotnet publish backend/NexusJob.Host`, then copies `dist/` → `wwwroot/`; stage 3 `dotnet/aspnet:10.0` runtime, `EXPOSE 8080`, `ENTRYPOINT ["dotnet","NexusJob.Host.dll"]` -- single same-origin image (AD-12).
- [x] `docker-compose.yml` -- `app` (build `.`, `8080:8080`, `ConnectionStrings__Postgres` env, `depends_on` postgres `service_healthy`) + `postgres` (`postgres:18`, `POSTGRES_*` env, `pg_isready` healthcheck, named volume) -- local stack (AR-14).
- [x] `.dockerignore` + `.gitignore` -- exclude `bin/ obj/ node_modules/ dist/ wwwroot/ .env *.user` -- clean build context and repo; no secrets committed.
- [x] `backend/NexusJob.Host/appsettings.json` -- non-secret defaults only; the connection string is supplied by env (`ConnectionStrings__Postgres`) -- config-from-environment (AD-22).
- [x] `.github/workflows/ci.yml` -- `on: [push, pull_request]`; job `backend` (`actions/setup-dotnet@v4` 10.x → `dotnet build` → `dotnet test`); job `frontend` (`actions/setup-node@v4` 24.x → `npm ci` → `npm run lint` → `npm run build`); job `image` (`needs: [backend, frontend]` → `docker build .`) -- every gate in CI; any failure fails the run (AR-13); no deploy/push.
- [x] `README.md` -- one paragraph: how to run (`docker compose up`), test (`dotnet test backend/NexusJob.sln`), and lint (`npm run lint` in `frontend/`) from a fresh checkout.

**Acceptance Criteria:**
- Given a clean checkout, when `dotnet build backend/NexusJob.sln` runs, then all eight projects compile with no errors, and only `NexusJob.Host` references a module implementation project.
- Given the solution, when `dotnet test` runs, then the ArchUnitNET suite passes on the clean tree; and when any one of the four AD-2 violations is introduced, the suite fails with a message naming that rule.
- Given `frontend/`, when `npm ci && npm run build` runs, then it produces `dist/`, all six FSD layer folders are present, and `npm run lint` exits `0` on the clean tree but non-zero when an upward cross-layer import is introduced.
- Given `docker compose up`, when the stack is healthy, then `GET http://localhost:8080/health` returns `200` with the liveness + `SELECT 1` JSON body, a non-API route returns the SPA `index.html` from the same origin, and the Host contains no CORS configuration.
- Given the running Host, when it handles a request, then each log line is structured JSON on stdout with no cookie, `Authorization` header, password, or hash value, and the DB connection string is read only from environment / configuration with nothing secret committed.
- Given `.github/workflows/ci.yml`, when parsed, then it defines backend build + architecture tests, frontend lint + build, and container-image build, with no step permitted to fail silently (validated by YAML/workflow lint + inspection; first live run awaits a GitHub remote).

## Implementation Notes

### Boundary-gate failure messages (manual verification, 2026-09-06)

Verified the hard way: each AD-2 violation was introduced as a **bare
`<ProjectReference>` with no type usage** (rules 1-3) or a bare source line
(rule 4) against the clean tree, `dotnet test backend/NexusJob.sln` was run with
no prior build, the rule-named failure recorded, and the change reverted. Bare
project references matter because the C# compiler elides an unused reference from
the emitted assembly, so the ArchUnitNET / `GetReferencedAssemblies()` checks
(which need real type usage) do not see it - the deterministic `*.csproj`
`<ProjectReference>`-graph scan in `ProjectReferenceRules.cs` does.

1. **Module impl -> another module's impl.** Bare `<ProjectReference>`
   `NexusJob.Modules.JobPostings -> NexusJob.Modules.Identity`. Fires rule 1
   **and** rule 3:
   - `AD-2 rule 1: no module implementation may depend on another module's non-Contracts assembly. A module implementation project may reference only NexusJob.Modules.*.Contracts. Offending <ProjectReference> element(s): NexusJob.Modules.JobPostings -> NexusJob.Modules.Identity`
   - `AD-2 rule 3: only NexusJob.Host may reference a module implementation assembly. Offending <ProjectReference> element(s): NexusJob.Modules.JobPostings -> NexusJob.Modules.Identity`
2. **`.Contracts` -> an impl.** Bare `<ProjectReference>`
   `NexusJob.Modules.JobPostings.Contracts -> NexusJob.Modules.Identity`. Fires
   rule 2 **and** rule 3:
   - `AD-2 rule 2: a .Contracts assembly must not depend on any module implementation. Offending <ProjectReference> element(s): NexusJob.Modules.JobPostings.Contracts -> NexusJob.Modules.Identity`
   - `AD-2 rule 3: only NexusJob.Host may reference a module implementation assembly. Offending <ProjectReference> element(s): NexusJob.Modules.JobPostings.Contracts -> NexusJob.Modules.Identity`
3. **Non-Host project -> an impl.** Bare `<ProjectReference>` from
   `NexusJob.ArchitectureTests` to `NexusJob.Modules.Identity`.
   - `AD-2 rule 3: only NexusJob.Host may reference a module implementation assembly. Offending <ProjectReference> element(s): NexusJob.ArchitectureTests -> NexusJob.Modules.Identity`
   - (the ArchUnitNET / assembly-reflection rule-3 check does **not** fire for a
     bare reference - only the csproj-graph scan catches it, which is the gap this
     file closes.)
4. **Cross-schema raw SQL.** Added, inside `NexusJob.Modules.Applications`, a
   source line containing `ExecuteSqlRaw("UPDATE identity.company_account SET display_name = %s")`.
   - `AD-2 rule 4: raw SQL (FromSqlRaw / ExecuteSql*) must not name a schema outside the caller module's own schema. Offending call(s): _TempRawSql.cs: ExecuteSqlRaw(... "identity.company_account" ...)`

The earlier type-usage forms of 1-3 (a `<ProjectReference>` **plus** a `typeof`
of a marker in the target) also fail, additionally tripping the ArchUnitNET rule
(`"Types that reside in assembly "NexusJob.Modules.JobPostings..." should not
depend on any Types that reside in assembly "NexusJob.Modules.Identity..." because
AD-2 rule 1: ..." failed`) and the `GetReferencedAssemblies()` check
(`... Offending references: NexusJob.Modules.JobPostings -> NexusJob.Modules.Identity`).

FSD lint negative check: adding `import '../features'` to a file in `src/entities/`
(and `import '../widgets'` to a file in `src/shared/`) makes `npm run lint` exit
non-zero with
`There is no policy allowing dependencies from elements of type "entities" to elements of type "features"  boundaries/dependencies`.

### Notable implementation decisions

- **Deterministic `*.csproj` `<ProjectReference>` scan.** `ProjectReferenceRules.cs`
  parses every `*.csproj` under `backend/` and asserts AD-2 rules 1-3 on the raw
  `<ProjectReference>` graph, independent of whether a target type is used. This
  is required because an unused project reference is elided from the compiled
  assembly, so the ArchUnitNET rules and the `GetReferencedAssemblies()` checks
  (kept as belt-and-braces for real type usage) would miss a declared-but-latent
  bad reference. The scan looks only at `<ProjectReference>` elements, so the
  `<BoundaryScanProject>` MSBuild inputs in the test csproj are naturally ignored.
- **`dotnet test` staleness.** `dotnet test backend/NexusJob.sln` builds only the
  test project and its reference graph, which (by design) excludes the modules, so
  a bare `dotnet test` would otherwise scan stale module DLLs. `NexusJob.ArchitectureTests.csproj`
  carries an `EnsureBoundaryTargetsBuilt` MSBuild target (`BeforeTargets="Build"`,
  an `<MSBuild>` task invocation of the Host + 3 module projects) so the scan
  always runs against current assemblies. This is not a `ProjectReference`: no
  assembly reference is added and the project stays off the "references an
  implementation" list. CI runs `dotnet build` then `dotnet test --no-build`, so
  the target is belt-and-braces there.
- **`eslint-plugin-boundaries` 7.x API.** v7 replaced the pre-7
  `element-types` / `rules` / string-selector API (the shape in the spec's Design
  Notes) with `dependencies` / `policies` / entity selectors. `eslint.config.js`
  uses the v7 API; the encoded layer order (`app -> pages -> widgets -> features ->
  entities -> shared`, downward only) and `error` severity are identical. Module
  resolution for extensionless barrel imports needs `settings['import/resolver']`
  = `{ typescript: ... }` (devDep `eslint-import-resolver-typescript`).
- **Frontend entry placement.** The placeholder component is `src/app/App.tsx`
  (FSD-idiomatic) with a thin `src/main.tsx` bootstrap importing it, rather than a
  root `src/App.tsx`.
- **`docker-compose.yml` Postgres volume.** `postgres:18` rejects a bind at
  `/var/lib/postgresql/data`; the named volume is mounted at `/var/lib/postgresql`.
- **Runtime image** installs `libgssapi-krb5-2` so Npgsql's startup GSSAPI probe
  does not emit a non-JSON stderr line.

### Verification performed (2026-09-06)

- `dotnet build backend/NexusJob.sln` -> Build succeeded, 9 projects, 0 warnings, 0 errors.
- `dotnet test backend/NexusJob.sln` -> `NexusJob.ArchitectureTests` 21/21 (adds
  synthetic-graph fixture facts for AD-2 rules 1-3, an `Async`/commented-out
  raw-SQL fixture, and a module-registry-consistency fact) + `NexusJob.Host.Tests`
  1/1 (the `/health` 503 DB-down path, `WebApplicationFactory`-based). All four
  AD-2 violations still produce rule-named failures when introduced as a bare
  `<ProjectReference>` / bare source line.
- `cd frontend && npm ci && npm run lint && npm run test:fsd-gate && npm run build`
  -> lint exits 0, the FSD-gate negative test confirms an upward
  `entities -> features` import is rejected via `boundaries/dependencies`, six FSD
  layer folders present, `dist/` produced.
- `docker compose up` (image runs as non-root `uid=1654`) -> `GET /health`
  `200 {"status":"healthy","database":"ok"}`; the compose `app` healthcheck goes
  `healthy`; DB-down path returns `503 {"status":"unhealthy","database":"unreachable"}`;
  `/` returns the SPA `index.html` from the same origin; no `Access-Control-*`
  headers; container logs are one structured JSON line per request with no
  cookie / Authorization / password / hash value.
- `npx @action-validator/cli .github/workflows/ci.yml` -> valid.

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-06) — blind-hunter, edge-case-hunter, verification-gap

Group A — AD-2 rule 4 (raw-SQL scan) coverage holes:
- **medium** `RawSqlSchemaScan.RawSqlCall` regex (`ExecuteSql|FromSqlRaw|…` + `\(`) — verified: `ExecuteSqlRawAsync`/`ExecuteSqlInterpolatedAsync`/`FromSqlRawAsync`/`SqlQueryRaw`/`FromSql` are never matched (`Async`/other name sits before the paren); the common async form of a cross-schema call passes the gate silently. → patch.
- **low** `RawSqlSchemaScan` scans raw source without stripping comments — a commented-out `ExecuteSqlRaw("identity.x")` in a module `.cs` breaks the build (false positive on the primary gate → developers distrust it). → patch (bundle with A).
- **false** split/concatenated string literal evades the schema-name regex (`"job_postings" + ".x"`) — AD-5 explicitly accepts that a determined raw call is caught only by review; rule 4 is a documented best-effort backstop. Refutation: the spec frames this exact limitation as acceptable.
- **false** `(`/`)` inside a SQL string literal miscounts `ExtractBalancedParenthesised` depth — same best-effort framing; contrived input, fix adds string-aware scanning complexity for negligible everyday risk.

Group B — boundary gates lack synthetic negative-case tests:
- **medium** (verification-gap, pre-verified) `ProjectReferenceRules` rules 1–3 and `BoundaryRules` ArchUnit/reflection facts assert `offenders.Count == 0` against the real clean tree only; no `[Fact]` drives a synthetic bad graph. A future edit to `IsImplementation`/`IsContracts` or an inverted `.Where` predicate makes the gate a no-op and CI stays green. `RawSqlSchemaScan` has fixture coverage; rules 1–3 do not. → patch.
- **medium** frontend `boundaries/dependencies` has no automated negative test — the FSD half of the SM-2 deliverable is only ever exercised by a manual "add one upward import" check; a regression that neuters the rule passes CI. → patch (bundle with B).
- **medium** (verification-gap, pre-verified) `GET /health` DB-down path (`503` + `{status:unhealthy,database:unreachable}`) has no automated test though `public partial class Program;` is already in place for a `WebApplicationFactory` unit test that needs no container. → patch (bundle with B). DB-up path → defer (needs real Postgres; spec scopes integration tests to later stories).

Group C — `/health` endpoint robustness:
- **medium** `Program.cs` `/health` sets no connect/command timeout — Postgres accepting TCP but stalling makes the liveness probe block ~15s (Npgsql connect default) / ~30s (command default). → patch.
- **medium** `Program.cs` reads `GetConnectionString("Postgres")` with no null/empty guard — an unconfigured deployment (`ConnectionStrings__Postgres` unset) is indistinguishable from a DB outage: app starts, `/health` returns `503` forever, nothing in the logs names the cause. I/O matrix covers "DB down" but not "not configured". → patch.
- **low** `/health` `catch (Exception)` also catches `OperationCanceledException` from a client disconnect → spurious unhealthy signal/log. Fix is a one-line exception filter (direct correction, no added surface). → patch (bundle with C).
- **false** `/health` discards the `SELECT 1` scalar instead of asserting `== 1` — if `ExecuteScalarAsync` returns without throwing, connectivity is proven and the value is invariantly 1; asserting it guards nothing.

Group D — `Program.cs` wwwroot / content-root handling:
- **low** `Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"))` (line 3) uses CWD while `UseStaticFiles`/`MapFallbackToFile` resolve against `IWebHostEnvironment` content root; identical in the shipped container (`WORKDIR /app`) but they diverge when launched with a different CWD, silently 404-ing SPA routes. Fix: derive the path from `builder.Environment.ContentRootPath`. → patch.
- **low** `CreateDirectory` throws on a read-only root filesystem before `builder.Build()` with no diagnostic — not on the shipped-image path (Dockerfile bakes `wwwroot`, and `CreateDirectory` is a no-op when it exists); fix adds a guard for an undemonstrated state. → reject.
- **low** `MapFallbackToFile("index.html")` 404s when `wwwroot/index.html` is absent (bare `dotnet run`) — spec's Verification exercises the SPA path via `docker compose`, not bare `dotnet run`; fix adds a branch for a non-target path. → reject.

Group E — `ModuleAssemblies` path resolution is brittle:
- **medium** `BackendDirectory` = fixed `Parent!.Parent!.Parent!.Parent!` off `AppContext.BaseDirectory`, and `LoadAll` derives tfm/config from directory names — all assume no `<rid>` segment. A RID-specific build or `dotnet publish`-based test run yields a wrong dir (empty project scan / failed `Assembly.LoadFrom`) or an NRE. Works for the current `dotnet test` path only. → patch (walk up to the dir containing `NexusJob.sln`).
- **low** `ProjectReferenceRules.The_Host_project_references_every_module_implementation_project` uses `Projects.Single(Host)` — throws a bare `InvalidOperationException` instead of a rule-named failure if resolution finds zero/two matches (only reachable if E's path resolution breaks). → patch (bundle with E: `SingleOrDefault` + explicit assert).

Group F — arch-test suite dead/misleading test + future-module drift:
- **low** `BoundaryRules.Only_the_Host_depends_on_a_module_implementation_ArchUnit` iterates `Contracts × Implementations` with `.Because(Rule3)` — byte-identical to the Rule 2 ArchUnit fact, relabelled; never inspects a non-Host/non-Contracts project, so it does not test rule 3 (which the reflection fact and `ProjectReferenceRules` do cover). Misleading, zero coverage value. → patch (delete or make it real).
- **medium** `ImplementationNames` / `ContractNames` / `RawSqlSchemaScan.ModuleSchemas` are three hand-maintained parallel lists with no test that fails when a new `NexusJob.Modules.*` project is added but not registered in all three — a silent gate blind spot for every future module. → patch (drift test enumerating `backend/NexusJob.Modules.*`).

Group G:
- **medium** `frontend/tsconfig.app.json` / `tsconfig.node.json` set `noUnusedLocals` etc. but not `"strict": true` — the standard Vite `react-ts` template (which the spec's task names as the scaffold source) sets it, and its absence is inconsistent with the backend's `Nullable enable` + `TreatWarningsAsErrors`; every future frontend story is then written without `strictNullChecks`/`noImplicitAny`. Fix: one line in each file. → patch.

Group H — container / CI hardening:
- **medium** `Dockerfile` runtime stage never switches off root — `mcr.microsoft.com/dotnet/aspnet:10.0` ships a non-root `app` user (`$APP_UID`); a "deployable" image running as root is a standard hardening gap. Fix: `USER $APP_UID` after the `apt-get` step. → patch.
- **low** no `healthcheck:` on the compose `app` service (and no Dockerfile `HEALTHCHECK`) though `/health` with a `SELECT 1` probe is the operability deliverable — compose gates `app` on `postgres` health but nothing consumes `app`'s own. → patch (compose `app` healthcheck hitting `/health`).
- **medium** `.github/workflows/ci.yml` builds the image but never starts it — the operability floor the spec's Verification exercises (`docker compose up && curl …/health`) is proven by manual check only; a `/health` regression reaches later stories with green CI. → patch (CI step: `docker compose up -d`, poll `/health`, tear down).
- **low** `ci.yml` has no `permissions:` block (GITHUB_TOKEN keeps broad default scope), no `concurrency:` group, no `timeout-minutes:` — standard workflow hygiene; low blast radius here but `permissions: contents: read` is the baseline. → patch (bundle with H).
- **reject** CI not documented as a required status check — branch protection is a GitHub server setting, not a repo artifact; the spec already notes "first live run awaits a GitHub remote". Fix would only edit docs/spec.

Group I:
- **low** no `.editorconfig` though `Directory.Build.props` sets `EnforceCodeStyleInBuild=true` (which enforces nothing without one) alongside `TreatWarningsAsErrors` (which turns any future non-silent style rule into a build break on an SDK bump) — the "machine-checked consistency" knob is inert or a latent footgun. Fix: add a minimal `.editorconfig`, or drop the prop — smallest coherent choice. → patch.

Group J:
- **low** Node major is pinned in `Dockerfile` (`node:24-alpine`) and CI (`node-version: '24'`) but nothing (`.nvmrc` / `engines.node`) holds a local checkout to it. Fix: add `frontend/.nvmrc`. → patch.

Group K — `eslint.config.js` robustness (gate verified working today):
- **low** `downwardPolicies` map yields `anyOf: []` for the last layer (`shared`, `slice(index+1)` empty) — verified harmless (config loads, gate fires correctly, and a separate explicit `shared→shared` policy + `default:'disallow'` cover the real need), but untidy. Fix: `flatMap` to drop the empty entry (behaviour-neutral simplification). → patch.
- **low** `boundaries/elements` `pattern: 'src/${layer}'` has no `/**` — verified: elements ARE typed and an upward import IS rejected with this shape (manual check), so the "silently enforces nothing" outcome does not occur; `src/${layer}/**` is a robustness tweak only. → patch (bundle with K).

Rejected / deferred (not patched this pass):
- **reject** `.gitignore` doesn't ignore `.claude/` — `.claude/` holds committed project skills and is intentionally tracked; `.claude/scheduled_tasks.lock` churn is pre-existing harness noise (already tracked+modified at baseline). `TestResults/`/`artifacts/`/`*.log` additions are speculative (no `--logger trx` in use).
- **reject** no `.env.example` — nice-to-have; README documents the one variable.
- **reject** `RawSqlSchemaScan` doesn't scan `NexusJob.Host` — the Host is the composition root, not a module, and legitimately owns the `public` schema; AD-5's "a module writes only its own schema" does not bind it.
- **reject** `dotnet test <sln>` + `EnsureBoundaryTargetsBuilt` double-build / output-lock risk — did not manifest across repeated `dotnet test` runs (clean, `--no-build`, and with injected violations); fix is speculative.
- **reject** Docker base images / `libgssapi-krb5-2` unpinned by digest — floating minor tags are common practice, the spec is silent, and digest-pinning is ongoing maintenance (Renovate/Dependabot territory).
- **false** `baseline_commit` "is 39 hex chars" — it is a valid 40-char SHA-1 (`13b791a59c547cbeb7b6ed00d6dd639f2dadeca1`).
- **defer** `GET /health` DB-up path has no automated test (needs a real Postgres; spec scopes Testcontainers/integration tests to later stories).
- **defer** no Vite dev-server `/api` proxy and no non-Docker dev loop — `npm run dev` cannot reach the Host; no current consumer (placeholder App makes no API calls), story 1.3 will need it.
- **defer** `eslint-plugin-react-hooks` / `react-refresh` not configured (standard Vite React template includes them) — nothing hook-shaped to lint yet; story 1.2 adds the shell.
- **defer** `sprint-status.yaml` carries a truncated story key `1-2-from-scratch-design-system-and-the-role-aware-application-sh` — pre-existing, generated by sprint-planning; does not affect story 1.1 resolution.
- **reject** no path alias for FSD imports (`../shared` works) — future ergonomics, not a defect.

### Pass 2 (2026-09-06) — code-review (branch diff, residual-gap focus)

All six findings were edge cases / CI hygiene, none blocking. All patched:

- **patch** `Program.cs` — a syntactically invalid `ConnectionStrings__Postgres` made `NpgsqlConnectionStringBuilder` throw at top-level scope (before `AddJsonConsole` takes effect), crashing the Host with an unstructured stack trace. Pass 1 handled "not configured"; "configured but unparseable" now degrades to the same start-and-report-unhealthy path via a `catch (ArgumentException or FormatException)` in `BuildProbeConnectionString`, plus a distinct startup `LogError`. Test: `Health_returns_503_when_the_connection_string_is_present_but_malformed`.
- **patch** `Program.cs` — `MapFallbackToFile("index.html")` served the SPA shell with `200 text/html` for unknown `/api/*` routes, contradicting the "non-API route" comment and breaking any JSON client once story 1.3 lands. Added `app.MapFallback("/api/{**rest}", () => Results.NotFound())` ahead of it (more specific template wins between fallbacks; real endpoints still win over both). Test: `Unknown_api_route_returns_404_and_not_the_spa_shell`.
- **patch** `RawSqlSchemaScan.RawSqlCall` — alternation listed `SqlQueryRaw` but not EF Core's interpolated `SqlQuery`, so a cross-schema `db.Database.SqlQuery<T>($"… job_postings.x …")` passed the AD-2 rule-4 gate. Added `SqlQuery` after `SqlQueryRaw`. Test assertion folded into `Detector_flags_the_async_raw_sql_forms`.
- **patch** `RawSqlSchemaScan` comment stripping — Pass 1's whole-source `//` / `/* */` blanking (added to kill false positives on commented-out calls) also truncated a real string literal containing `//` (a URL) or `/* */` (an inline SQL comment), hiding a later cross-schema name. Replaced the two `Regex.Replace` calls with `StripCommentsPreservingStringLiterals`, which copies C# string literals (`"`, `$"`, `@"`, `$@"` / `@$"`) through verbatim. Test: `Detector_still_flags_a_cross_schema_name_after_an_inline_sql_comment_marker`; the commented-out-call false-positive tests still pass.
- **patch** `ci.yml` — `on: [push, pull_request]` with no branch filter ran the full pipeline (incl. the ~20-min image build + smoke test) twice for every push to a branch with an open PR (the two events have distinct concurrency groups and don't cancel each other). Scoped `push` to `branches: [main]`; `pull_request` stays unfiltered. Trade-off: a branch push with no open PR no longer triggers CI — CI runs once the PR exists.
- **patch** `ci.yml` smoke test — `docker compose down -v` ran under `set -euo pipefail` without `|| true`, so a teardown failure preempted the explicit `::error::` annotation and `exit 1` for a real `/health` failure. Added `|| true` to match the adjacent `docker compose logs` line.

## Design Notes

- **Why the arch-test project has no project references.** ArchUnitNET loads assemblies from disk. A compile-time reference to the module DLLs would make `NexusJob.ArchitectureTests` itself violate AD-2 rule 3 ("only the Host references an implementation"). Load them with `Assembly.LoadFrom` against the build-output path instead.
- **`eslint-plugin-boundaries` flat config.** ESLint 9 uses `eslint.config.js`. The `boundaries/element-types` rule needs `default: "disallow"` plus one `allow` entry per layer; set it to `"error"` so `eslint .` exits non-zero. Golden shape:
  ```
  { type: 'app',      allow: ['pages','widgets','features','entities','shared'] }
  { type: 'pages',     allow: ['widgets','features','entities','shared'] }
  { type: 'widgets',   allow: ['features','entities','shared'] }
  { type: 'features',  allow: ['entities','shared'] }
  { type: 'entities',  allow: ['shared'] }
  { type: 'shared',    allow: ['shared'] }
  ```

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln` -- expected: success, eight projects, no errors.
- `dotnet test backend/NexusJob.sln` -- expected: all architecture tests green on the clean tree.
- `cd frontend && npm ci && npm run lint && npm run build` -- expected: lint exits `0`, `dist/` produced.
- `docker compose up --build -d && curl -fsS localhost:8080/health && curl -fsS localhost:8080/ && docker compose down` -- expected: `200` + `{"status":"healthy","database":"ok"}`; the second curl returns SPA HTML.
- `npx --yes @action-validator/cli .github/workflows/ci.yml` -- expected: valid workflow schema.

**Manual checks:**
- Introduce each of the four AD-2 violations one at a time (e.g. a `ProjectReference` from `Identity.Contracts` to `NexusJob.Modules.Identity`), run `dotnet test`, confirm a rule-named failure, revert; record the four messages in Implementation Notes. Do the same for one upward FSD import against `npm run lint`.
- Inspect the Host's stdout for one request: no `Set-Cookie` / `Authorization` / `password` substring appears.
