---
title: 'OpenAPI 3.0 document and the NSwag TypeScript client pipeline'
type: 'feature'
created: '2026-09-07'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'b946090c3582d8314ac880c3e0b70675e23a61f9'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.3a shipped the `/api/auth/*` endpoints but the Host serves no OpenAPI document and the frontend has no generated API client, so any frontend that calls the API (starting with 1.3b-ii's Sign up / Log in surface) would have to hand-write request/response types — forbidden by AD-15.

**Approach:** Have the Host emit one pinned OpenAPI **3.0** document (`Microsoft.AspNetCore.OpenApi`), also written to a file at build time. Add an NSwag step that generates a TypeScript client from that document into `frontend/src/shared/api/`, commit the generated client, and add a CI gate that regenerates it and fails on any drift. This is story 1.3b-i; consuming the client from an `entities/*`/`features/*` slice and the Sign up / Log in UI are 1.3b-ii (see `deferred-work.md`).

## Boundaries & Constraints

**Always:**
- AD-15: exactly **one** OpenAPI document, **3.0** output (the .NET 10 generator can emit 3.1; it must be pinned to 3.0 — NSwag's TS generator is unreliable on 3.1). The TypeScript client is generated **from that document** and is the only client-side description of the API; hand-written request/response types that duplicate it are forbidden.
- AD-16: the generated client lives under `frontend/src/shared/api/`. (The rule that only `entities/*/api` / `features/*/api` may import it is enforced when the first consumer lands in 1.3b-ii.)
- The generated client is committed to the repo (so `tsc` and local dev work offline) **and** CI regenerates it and fails if the committed copy is stale.
- `dotnet build` / `dotnet test` stay green (0 warnings, `TreatWarningsAsErrors`); the existing ArchUnitNET boundary gates, Host tests, and Identity integration tests still pass.
- `npm run lint`, `npm run test:fsd-gate`, `npm run lint:tokens`, `npm run build`, and `npm test` stay green. The generated client is `eslint`-ignored (generated code) but still type-checked by `tsc`.
- The build-time OpenAPI JSON file is a build artifact — git-ignored, never committed.

**Never:**
- No frontend feature code, no `entities/session`, no `features/auth`, no `pages/sign-in`, no TanStack Query, no Vite dev proxy, no shell changes — all 1.3b-ii.
- No second OpenAPI document, no Swagger UI / `SwaggerUI` middleware (the JSON endpoint only), no API versioning scheme.
- No change to any `/api/auth/*` endpoint behaviour or its 1.3a metadata beyond what is needed for an accurate document (e.g. an operation id or a missing `ProducesProblem`).
- No hand-editing the generated client file; no `@ts-nocheck` on it.
- No new runtime dependency in the Host beyond the OpenAPI packages; no CORS, no auth on the document endpoint decisions beyond "anonymous, same-origin".

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fetch the OpenAPI document | `GET /openapi/v1.json` on the running Host | `200 application/json`; top-level `openapi` is `"3.0.x"`; `paths` contains `/api/auth/register`, `/api/auth/login`, `/api/auth/me`, `/api/auth/logout`, `/api/auth/csrf` with their 1.3a status codes / request+response schemas | N/A |
| Build-time document emission | `dotnet build backend/NexusJob.Host` | the same 3.0 document is written to the configured build output path (git-ignored) | build fails if generation errors |
| Generate the client from a current document | `npm run generate:api` after a Host build | `frontend/src/shared/api/` contains a TypeScript client (client class + request/response types) for the five auth operations; re-running produces no diff | script exits non-zero on generator error |
| Client drift | committed `frontend/src/shared/api/` no longer matches what the current document generates | the CI drift gate regenerates and `git diff` is non-empty → job fails with a "run `npm run generate:api` and commit" message | job exits non-zero |
| Type-check with the generated client present | `npm run build` (`tsc -b` + `vite build`) | compiles clean under `strict`; the generated client is in the `tsc` graph | `tsc` errors fail the build |
| Lint with the generated client present | `npm run lint` | passes; `src/shared/api/**` is in the eslint `ignores` list, everything else is still linted | N/A |

## Resolved Decisions

- **NSwag runs from `frontend/` via npm, drift-checked in a dedicated CI job.** `nswag` is a `frontend` devDependency; `frontend/nswag.json` takes the Host's build-emitted JSON as input (relative path) and `npm run generate:api` drives it. A new CI job (`openapi-client`) sets up **both** .NET and Node, runs `dotnet build` on the Host to emit the JSON, `npm ci` + `npm run generate:api` in `frontend`, then `git diff --exit-code -- frontend/src/shared/api` and fails with a "run `npm run generate:api` and commit" message on any drift. The OpenAPI JSON stays an untracked build output; only the generated TS client is committed. (Not chosen: `NSwag.MSBuild` writing into the frontend tree from `dotnet build`; not chosen: committing the JSON as a checked-in pipeline input.)

</frozen-after-approval>

## Code Map

- `backend/Directory.Packages.props` -- add `Microsoft.AspNetCore.OpenApi` and `Microsoft.Extensions.ApiDescription.Server` (v10 line), pinned.
- `backend/NexusJob.Host/NexusJob.Host.csproj` -- reference both; set the MSBuild props that turn on build-time document generation and point it at a git-ignored output path (e.g. `openapi/`).
- `backend/NexusJob.Host/Program.cs` -- `builder.Services.AddOpenApi("v1", o => o.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0)`; `app.MapOpenApi()` (serves `/openapi/v1.json`, anonymous, before the `MapFallback`/SPA fallback so it is not shadowed). No other Program.cs change.
- `backend/NexusJob.Modules.Identity/IdentityModule.cs` -- only if the document needs it: add `.WithName(...)` operation ids and any missing `.ProducesProblem(...)` on the five auth endpoints so the generated client's method names and error types are sane. Behaviour unchanged.
- `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` -- NEW. `GET /openapi/v1.json` → `200`, `openapi` starts `"3.0"`, `paths` has the five `/api/auth/*` entries. Uses the existing `IdentityApiFixture` (no new fixture).
- `.gitignore` (root or `backend/`) -- ignore the Host's build-emitted OpenAPI JSON path.
- `frontend/package.json` -- add `nswag` (or the resolved tool) as a devDependency; add `"generate:api"` script.
- `frontend/nswag.json` -- NEW. NSwag config: input = the Host build's JSON (relative path), output = `src/shared/api/<name>.ts`, `Fetch` template, `generateClientClasses: true`, injectable `fetch`/`baseUrl` on the client constructor (so 1.3b-ii can supply `credentials: 'include'` + the `X-CSRF-TOKEN` header), `dateTimeType: string`, generate an `ApiException` class.
- `frontend/src/shared/api/` -- NEW. The generated client file (committed) + a hand-written `index.ts` barrel (NOT generated, NOT ignored) re-exporting the client class and the request/response types.
- `frontend/src/shared/index.ts` -- re-export `./api` so the layer has one import surface (keep `APP_NAME`, tokens, ui).
- `frontend/eslint.config.js` -- add `src/shared/api/**` to the top-level `ignores` array (generated code). Nothing else.
- `frontend/tsconfig.app.json` -- only if NSwag output trips `noUnusedLocals`/`noUnusedParameters`: exclude `src/shared/api` from those via a dedicated tsconfig or an NSwag option — never `@ts-nocheck`. Keep it in the type-check graph.
- `.github/workflows/ci.yml` -- add the `openapi-client` job (setup-dotnet + setup-node, build Host, `npm run generate:api`, `git diff --exit-code -- src/shared/api`); the existing `frontend` job's `npm run build` / `npm run lint` already cover "compiles / lints with the client present".
- `frontend/scripts/check-fsd-gate.mjs`, `check-tokens-gate.mjs` -- read-only reference (the drift gate follows the same "regenerate → assert no change" shape).
- `_bmad-output/implementation-artifacts/spec-1-3a-identity-backend-and-auth-endpoints.md` -- continuity: the five endpoints, their bodies (`{accountType,name,email,password}` / `{accountType,email,password}`), responses (`{id,accountType,displayName}` / `{token}`), and status codes (200/204/400/401/409) the document must describe.

## Tasks & Acceptance

**Execution:**
- [ ] `backend/Directory.Packages.props` + `backend/NexusJob.Host/NexusJob.Host.csproj` -- add `Microsoft.AspNetCore.OpenApi` + `Microsoft.Extensions.ApiDescription.Server`; enable build-time doc generation to a git-ignored path.
- [ ] `backend/NexusJob.Host/Program.cs` -- `AddOpenApi` pinned to 3.0 + `MapOpenApi()` (anonymous, ahead of the SPA fallback).
- [ ] `backend/NexusJob.Modules.Identity/IdentityModule.cs` -- add operation ids / missing `ProducesProblem` as needed for a clean generated client; no behaviour change.
- [ ] `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` -- assert the 3.0 document and the five auth paths.
- [ ] `.gitignore` -- ignore the build-emitted OpenAPI JSON.
- [ ] `frontend/package.json` + `frontend/nswag.json` -- NSwag devDep + config + `generate:api` script.
- [ ] `frontend/src/shared/api/*` -- run `generate:api`; commit the generated client + a hand-written `index.ts` barrel.
- [ ] `frontend/src/shared/index.ts` -- re-export `./api`.
- [ ] `frontend/eslint.config.js` -- `ignores` the generated client; `frontend/tsconfig.app.json` -- keep it type-checked (adjust only if NSwag output trips unused-symbol rules).
- [ ] `.github/workflows/ci.yml` -- the `openapi-client` regenerate-and-`git diff --exit-code` drift gate (both toolchains).

**Acceptance Criteria:**
- Given the running Host, when `GET /openapi/v1.json` is called, then it returns a `3.0.x` OpenAPI document that describes the five `/api/auth/*` operations with their 1.3a request bodies, response shapes, and status codes.
- Given `dotnet build backend/NexusJob.sln`, when it completes, then it is warning-free and the OpenAPI JSON is written to the git-ignored build path (and is not tracked by git).
- Given a checkout and a Host build, when `npm run generate:api` runs, then it (re)produces `frontend/src/shared/api/` and a second run leaves `git status` clean.
- Given CI, when the committed `frontend/src/shared/api/` does not match what the current document generates, then the drift-gate job fails and names the fix.
- Given the committed generated client, when `npm run build` and `npm run lint` run, then both pass — `tsc` type-checks the client, `eslint` skips it, and no other file is newly ignored.
- Given `dotnet test backend/NexusJob.sln`, when it runs, then `OpenApiDocumentTests` passes alongside the unchanged 1.3a suites.

## Implementation Notes

## Spec Change Log

## Review Triage Log

### Pass 1 (2026-09-07) — blind-hunter, edge-case-hunter, verification-gap

No intent_gap or bad_spec — no loopback. Six `patch` entries applied by the step-03 subagent; one Design Note corrected by the orchestrator; the rest rejected. Backend build 0W/0E and `dotnet test` (ArchitectureTests 22, Host.Tests 3, IntegrationTests 23) + the frontend gates were re-run green after the patches.

**patch:**
- `.github/workflows/ci.yml` `openapi-client` job (verification-gap, pre-verified; also blind-hunter, edge-case-hunter) — **the build-time OpenAPI JSON that NSwag actually consumes has no 3.0 assertion**. Only the *runtime* endpoint is pinned + tested (`OpenApiDocumentTests`); the build-time file's version rests solely on the `--openapi-version OpenApi3_0` MSBuild arg. Drop that arg (or an SDK change) and NSwag silently runs on a 3.1 document — the input AD-15 explicitly bans — with all-green CI. medium. Fix: after the Host build step, assert `backend/NexusJob.Host/openapi/NexusJob.Host.json` exists and its top-level `openapi` matches `3.0.*`.
- `.github/workflows/ci.yml` drift check (all three layers) — `git diff --exit-code -- src/shared/api` ignores **untracked** files, so a future NSwag version/config that emits an *additional* file into `src/shared/api/` passes the gate despite real drift. The spec's own Verification uses `git status --porcelain` (which catches it); the job did not match. low. Fix: `git add -N -- src/shared/api` before the `git diff --exit-code` (or use `git status --porcelain`).
- `frontend/eslint.config.js` (blind-hunter, edge-case-hunter) — `ignores: ['…','src/shared/api/**']` also un-lints the **hand-written** `src/shared/api/index.ts` barrel, contradicting the Code Map ("NOT ignored") and the AC ("no other file newly ignored") and removing the barrel from the FSD boundary rules. low. Fix: ignore only the generated `src/shared/api/nexus-api-client.ts`.
- `frontend/src/shared/index.ts` (edge-case-hunter) — `export * from './api'` re-exports the generated client through the top-level `shared` barrel, which invites `import … from 'shared'` in violation of AD-16's "only `entities/*/api` / `features/*/api` import the client", and risks a silent ambiguous-re-export drop if a generated name ever collides with a `tokens`/`ui` export. low-medium. Fix: remove that line; consumers import from the `shared/api` sub-path (its own barrel is the surface).
- `backend/NexusJob.IntegrationTests/OpenApiDocumentTests.cs` (blind-hunter, edge-case-hunter) — the test asserts only that five path keys exist; the AC promises the document "describes the five operations with their … status codes", and the entire `IdentityModule.WithName("Auth_*")` change is unverified. Also `GetProperty("openapi")` throws `KeyNotFoundException` rather than a descriptive failure on a partial document. low. Fix: `TryGetProperty` guards; assert the five `Auth_*` operation ids and each auth path's documented response status codes.
- `backend/NexusJob.Host/Program.cs` (edge-case-hunter) — `app.MapOpenApi()` is anonymous only because no fallback authorization policy exists; the comment says "anonymous" but nothing enforces it. low, defensive. Fix: `app.MapOpenApi().AllowAnonymous()`.

**Design Note corrected (orchestrator, not a code change):** the "Client constructor stays injectable" note described a `{ fetch?, baseUrl? }` shape; the generated `AuthClient` is `constructor(baseUrl?, http?)` with `http` defaulting to `window`. Corrected so 1.3b-ii planning uses the real signature.

**rejected:**
- edge-case-hunter — "build-time doc emission runs `Program.cs` to `app.Run()`, so the DB-less `openapi-client` job / `dotnet build` fail or hang on the 1.3a startup migration": **false**. The 1.3a startup migration is `try/caught` (logs + continues) and the placeholder connection string carries `Timeout=3`; `dotnet build` with no database succeeds 0W/0E (orchestrator's own run) and `docker compose build app` exits 0 (subagent). Only JSON log noise, no failure, no hang.
- blind-hunter — "login response shape is ambiguous / the committed client may be wrong": **false**. Verified against shipped 1.3a — `POST /api/auth/login` returns `AuthAccountResponse { id, accountType, displayName }` (only `GET /api/auth/csrf` returns `{ token }`); the generated `login(): Promise<AuthAccountResponse>` is correct.
- edge-case-hunter — "the committed client blob may be CRLF, making the LF-pinned Linux drift gate permanently red": **false**. `git ls-files --eol` reports `i/lf w/lf` for both `src/shared/api` files with `attr/text eol=lf` applied; the local drift gate was run clean on Windows.
- blind-hunter — "no cross-check that the build-time JSON is byte-identical to what `MapOpenApi` serves": low. Formatting differences between the `GetDocument` tool and `MapOpenApi` are common and benign; the version (the part that matters for NSwag) is now asserted on the build-time file directly.
- blind-hunter — "`/health` is in the committed client surface": low. Sanctioned by the Design Note ("may appear … harmless"); the churn risk (an unrelated health-metadata change dirtying the client) is hypothetical and one story away.
- blind-hunter — "generated DTOs are looser than needed (`[key: string]: any`, `accountType: string` not a union)": low. NSwag's default output shape; the C# `AuthAccountResponse.AccountType` is itself a `string`. Tightening (schema transformer / enum) is a follow-up for 1.3b-ii when it first consumes the types.
- blind-hunter — "generated file has no trailing newline → drift risk": low. Deterministic today (`newLineBehavior: LF`, repeated generation byte-identical); nothing in the pipeline formats the file, and there is no `.editorconfig` `insert_final_newline` affecting it.
- blind-hunter — "no README/CONTRIBUTING note for `npm run generate:api`": low. The `openapi-client` CI job and the `shared/api/index.ts` header comment document the flow; a missing Host build yields NSwag's own "file not found".
- blind-hunter — "`nswag.json` `typeScriptVersion: 5.4` vs `package.json` `typescript ~6.0.0`": low. The generated client compiles clean under the project's `strict` `tsc` (verified); `5.4` is NSwag 14.7.1's output target, forward-compatible with TS 6.
- blind-hunter — "`deferred-work.md` is 'Append-only' but the 1.3b entry was rewritten in place": low. The rewrite refined an as-yet-unactioned deferral into accurate 1.3b-i / 1.3b-ii entries; a stack of near-duplicate superseded entries would be less useful than the corrected text.
- blind-hunter — "new CI job has no `setup-dotnet` NuGet cache / rebuilds the Host cold / no `needs:` reuse": low. CI-time optimisation; correctness is unaffected and a broken regeneration is still caught (by the drift gate, then by the `frontend` job's `tsc`).
- edge-case-hunter / blind-hunter — "unverified version + moniker assumptions (`Microsoft.Extensions.ApiDescription.Server 10.0.11`, `nswag.json runtime: Net100`, the `--openapi-version` flag spelling)": **verified working** — `dotnet build` and `npm run generate:api` both succeed on the orchestrator's machine and emit `openapi: 3.0.4`.

## Design Notes

- **The generated client is treated as generated code:** committed for offline `tsc` + dev, `eslint`-ignored, never hand-edited, and CI is the source of truth via the drift gate (same philosophy as the FSD / token gates: regenerate, assert no change). A hand-written `index.ts` barrel is the stable public surface so 1.3b-ii imports `shared/api`, not a NSwag-named file.
- **Client constructor stays injectable.** As generated, `AuthClient`'s signature is `constructor(baseUrl?: string, http?: { fetch(url, init): Promise<Response> })` and `http` defaults to `window` (which is absent in Node/SSR — 1.3b-ii's Vitest specs must pass an `http` stub or run in jsdom). 1.3b-i generates it and supplies nothing. 1.3b-ii constructs the real instance with `baseUrl: ''` (same-origin, AD-12) and an `http` wrapper that sets `credentials: 'include'` (the auth cookie) and the `X-CSRF-TOKEN` header from `GET /api/auth/csrf` on mutating calls.
- **`/health` in the document:** it may appear (it is a mapped GET). That is harmless; the AC only pins the five auth paths. Do not add `[ExcludeFromDescription]` unless it causes a generator problem.
- **3.0 pin mechanism:** .NET 10's `AddOpenApi` exposes `OpenApiVersion`; set it to `OpenApiSpecVersion.OpenApi3_0` for both the served endpoint and the build-time file. If the two ever diverge, the drift gate catches it. Orval is the documented fallback if the 3.0 pin proves lossy (AD-15) — not this story.

## Verification

**Commands:**
- `dotnet build backend/NexusJob.sln --configuration Release` -- expected: 0 warnings; the OpenAPI JSON appears at the build path; `git status` does not show it.
- `dotnet test backend/NexusJob.sln --configuration Release` -- expected: all pass, including `OpenApiDocumentTests`.
- `dotnet run --project backend/NexusJob.Host` (or `docker compose up`), then `curl -s localhost:8080/openapi/v1.json | jq '.openapi, (.paths | keys)'` -- expected: `"3.0.x"` and the five `/api/auth/*` paths.
- `cd frontend && npm ci && npm run generate:api && git status --porcelain src/shared/api` -- expected: empty (a fresh generation matches the commit).
- `npm run lint` / `npm run build` / `npm test -- --run` -- expected: all pass with the generated client present.

**Manual checks:**
- Open the generated client: confirm it has typed methods for register / login / me / logout / csrf and named request/response interfaces, and no hand edits.
