# Technology-Currency Review — ARCHITECTURE-SPINE.md (NexusJob)

- **Target:** `../ARCHITECTURE-SPINE.md`
- **Review date:** 2026-09-05
- **Reviewer role:** technology-currency reviewer (verify every committed tech choice was reality-checked, not asserted from training data)
- **Method:** live web searches + primary-source fetches (release blogs, GitHub releases, NuGet, npm, vendor docs). URLs cited inline.

---

## One-line verdict

The spine's *architecture* is sound and its stack is mostly current, but the Stack table was **not** fully reality-checked on 2026-09-05 as it claims: three entries (TypeScript, PostgreSQL, React Router) are a full major version behind, and the spine misses a **real, breaking NSwag ⇄ .NET 10 OpenAPI 3.1 incompatibility** that directly threatens AD-15.

---

## Per-technology verification

Legend: ✅ current / 🟡 usable but not current / 🟠 works, caveat the spine omits / 🔴 wrong or materially better default exists.

### .NET / C# — claimed "10 (LTS) / 14"

- **Verified:** .NET 10 GA 2025-11-11, LTS, supported to 2028-11-10. C# 14 shipped with it. Current patch line is .NET 10.0.x.
- **Verdict:** ✅ Correct. LTS label correct. C# 14 pairing correct.
- Sources: https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/ , https://en.wikipedia.org/wiki/.NET

### ASP.NET Core (Minimal APIs) — claimed "10"

- **Verified:** ASP.NET Core 10 ships in .NET 10; Minimal APIs are the current recommended style; `Microsoft.AspNetCore.OpenApi` 10.0.x is the built-in OpenAPI stack (Swashbuckle removed from templates since .NET 9).
- **Verdict:** ✅ Correct — but see **Finding H-1** (OpenAPI 3.1 default) which is an ASP.NET Core 10 behaviour change the spine does not account for.
- Sources: https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0 , https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi

### Entity Framework Core + Npgsql provider — claimed "10"

- **Verified:** EF Core 10 GA with .NET 10 (LTS). `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x is published and current (latest 10.0.3 line); release notes document EF 10 JSON complex-type mapping and PG18 features.
- **Verdict:** ✅ Correct. Provider is actively maintained and version-aligned with EF 10.
- Sources: https://www.npgsql.org/efcore/release-notes/10.0.html , https://www.nuget.org/packages/npgsql.entityframeworkcore.postgresql/ , https://github.com/npgsql/efcore.pg/releases

### PostgreSQL — claimed "17"

- **Verified:** **PostgreSQL 18 went GA 2025-09-25** — roughly 11 months before this review. PG 18 headline: new async I/O subsystem (up to ~3× read throughput), UUIDv7 generation function, virtual generated columns, OAuth support. PG 17 remains fully community-supported (5-year window, EOL Nov 2029). PG 19 is expected ~Sept/Oct 2026.
- **Verdict:** 🟡 Not current. PG 17 is a safe, supported, conservative choice, but the spine claims "verified current on the web 2026-09-05" and 17 has not been the current major since before this project started. PG 18's native `uuidv7()` is directly relevant — the spine generates UUID v7 in app code (fine), but PG 18 would also allow DB-side default generation. No blocker; update the number or add a one-line "17 chosen deliberately over 18 for maturity" note.
- Sources: https://www.postgresql.org/about/news/postgresql-18-released-3142/ , https://www.postgresql.org/support/versioning/

### ArchUnitNET (CI boundary tests) — claimed "current (actively maintained; NetArchTest rejected — stale since 2023)"

- **Verified:** `TngTech.ArchUnitNET` latest 0.13.3 (2026-03-05), ~61 releases, regular cadence through 2025–2026 — actively maintained. Still pre-1.0 (API may shift on minor bumps — pin exactly). NetArchTest: last release 2023, effectively unmaintained; a community fork `NetArchTest.eNhancedEdition` exists. TNG also ships `ArchUnitNET.xUnit` / `.NUnit` adapters.
- **Verdict:** ✅ Correct pick and correct rationale. NetArchTest rejection is accurate. Minor: note the 0.x version — pin the exact version in the arch-tests project and expect occasional API churn on upgrades.
- Sources: https://github.com/TNG/ArchUnitNET/releases , https://www.nuget.org/packages/TngTech.ArchUnitNET/ , https://milanjovanovic.tech/blog/shift-left-with-architecture-testing-in-dotnet

### Input validation — claimed "built-in .NET 10 minimal-API validation"

- **Verified:** Real and shipped in .NET 10. `builder.Services.AddValidation()` registers an endpoint filter that validates request models via `System.ComponentModel.DataAnnotations` attributes, custom `ValidationAttribute`s, and `IValidatableObject`; source-generated (opt-in interceptor namespace) so no runtime reflection; returns RFC-9457-style 400 on failure. It **does** cover input validation for Minimal API endpoints.
- **Verdict:** ✅ Exists and covers the need. 🟠 Caveat the spine should state: it is **DataAnnotations-based** — no cross-field/conditional rule DSL, no async rules, weaker than FluentValidation for anything non-trivial. The spine already defers FluentValidation "until rule complexity justifies it", which is the right call, but the AD/Stack note should name the DataAnnotations limitation explicitly so a builder isn't surprised.
- Sources: https://timdeschryver.dev/blog/aspnet-10-validating-incoming-models-in-minimal-apis , https://www.nikolatech.net/blogs/minimal-api-validation-in-aspnet-core , https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0

### Testcontainers for .NET — claimed "current"

- **Verified:** `Testcontainers` (.NET) latest 4.14.0 (2026-08-14), repo active (commits through Sept 2026). Standard choice for throwaway Postgres in integration tests; pairs cleanly with the per-module `DbContext` + schema-per-module design.
- **Verdict:** ✅ Correct and current.
- Sources: https://www.nuget.org/packages/Testcontainers/ , https://github.com/testcontainers/testcontainers-dotnet/releases , https://dotnet.testcontainers.org/

### React — claimed "19.2.x"

- **Verified:** React 19.2 is the current stable minor (first published 2025-10-01; patch line in the 19.2.x range as of mid-2026). No React 19.3, no React 20. react.dev/versions lists 19.2 as latest.
- **Verdict:** ✅ Correct and current.
- Sources: https://react.dev/versions , https://github.com/facebook/react/releases

### TypeScript — claimed "5.x"

- **Verified:** **TypeScript 7.0 went GA 2026-07-08** — the native (Go) compiler port, ~8–12× faster builds, designed to be behaviourally compatible with 6.0. TypeScript 6.0 was the immediately-prior line. As of 2026-09, **5.x is two majors behind.** Note: 7.0 lacks the programmatic API that embedded-language tooling (Vue/Svelte/Angular/Astro/MDX) needs, so those ecosystems stay on 6.0 for now — **but a plain React + Vite SPA has no such blocker.**
- **Verdict:** 🟡 Stale. TypeScript is exceptionally backward-compatible so "5.x" is not *broken*, but it contradicts the spine's "verified current 2026-09-05" claim. Recommend "6.x (or 7.x once your Vite/ESLint/generator toolchain confirms 7.0 support)". At minimum bump to 6.x.
- Sources: https://devblogs.microsoft.com/typescript/announcing-typescript-7-0/ , https://devblogs.microsoft.com/typescript/announcing-typescript-7-0-rc/ , https://www.infoq.com/news/2026/08/typescript-7-released/

### Vite — claimed "8.x"

- **Verified:** Vite 8.0 GA 2026-03-12; latest 8.2.x as of Sept 2026. Vite 8 unifies on Rolldown (Rust bundler) replacing esbuild+Rollup. React plugin ecosystem supports it.
- **Verdict:** ✅ Correct and current. (Watch: Vite 8 + Rolldown is a big internal change; pin exact version and verify the OpenAPI-client generator's Vite plugin, if any, supports 8.)
- Sources: https://vite.dev/blog/announcing-vite8 , https://www.npmjs.com/package/vite?activeTab=versions

### TanStack Query — claimed "v5"

- **Verified:** For **React**, `@tanstack/react-query` is still **v5** (rolling date-tagged releases through 2026). A "v6" exists only for the Svelte and Solid adapters (framework-native rewrites); the React package and the shared query-core remain v5.
- **Verdict:** ✅ Correct for a React SPA. No action.
- Sources: https://github.com/TanStack/query/releases , https://tanstack.com/query/latest

### React Router — claimed "v7"

- **Verified:** **React Router v8 GA June 2026** (latest 8.3.x by late July 2026). v8 is a low-friction upgrade — v7 opt-in flags become permanent defaults; main hard requirement is Vite 7+ Environment API (satisfied by the spine's Vite 8). With v8, **React Router v6 and Remix v2 are EOL**; **v7 receives security updates only.**
- **Verdict:** 🟡 Not current. v7 is still security-supported so not urgent, but for a greenfield project starting Sept 2026 there is no reason to start on a version that is already in security-only maintenance. Recommend v8. If the team wants the plain client-side router only, also evaluate whether React Router is even needed vs. a lighter option — but v7→v8 is the minimal fix.
- Sources: https://remix.run/blog/react-router-v8 , https://reactrouter.com/upgrading/v7 , https://github.com/remix-run/react-router/releases

### OpenAPI → TS client generator — claimed "NSwag — pin exact version; budget for major-version upgrade breakage"

- **Verified:** NSwag is **actively maintained**: `NSwag.*` 14.7.1 (2026-04-20); 14.6.x line (Sept–Nov 2025) shipped explicit .NET 10 SDK support and fixes (issues #5169, #5216, #5301). MIT, ~100M+ downloads, still the default in the .NET ecosystem and in Visual Studio's "Connected Services" client generation.
- **However:** .NET 10's built-in OpenAPI generator **emits OpenAPI 3.1 by default** (JSON Schema 2020-12: `nullable:true` gone, `type` arrays, changed integer handling). NSwag's document reader/TS generator was built for 3.0 and has **known breakage against 3.1 output** (community reports of malformed/optional-everything TS types, integer schemas without `type`). This is exactly the "known-issue flag" to raise — and it lands squarely on **AD-15**, which hard-commits to "host emits a single OpenAPI document; TS client generated from it in CI (NSwag), the only permitted description of the API."
- **Ecosystem movement:** the JS-side OpenAPI-codegen field has moved since NSwag was the obvious .NET pick. Current front-runners for a React + fetch/React-Query SPA, all with first-class OpenAPI 3.1 support: **Hey API (`@hey-api/openapi-ts`)** (plugin architecture, successor to openapi-typescript-codegen), **Orval** (batteries-included, emits TanStack Query v5 hooks directly), **openapi-typescript + openapi-fetch** (types-only, zero runtime), **Kubb** (max flexibility). Orval in particular removes hand-written hook boilerplate the spine would otherwise carry.
- **Verdict:** 🟠 NSwag is not dead and not wrong, but the spine's "budget for major-version upgrade breakage" understates the risk: the breakage is **present today** against .NET 10's default OpenAPI output, not a future upgrade concern. See **Finding H-1** for options.
- Sources: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0 , https://github.com/RicoSuter/NSwag/discussions/5169 , https://github.com/RicoSuter/NSwag/issues/5216 , https://awstip.com/openapi-3-1-in-net-10-broke-our-swagger-eee5bf983dc6 , https://dev.to/nyaomaru/which-openapi-codegen-should-you-choose-openapi-typescript-vs-hey-api-vs-orval-vs-kubb-100p , https://kubb.dev/docs/5.x/guide/comparison

### FSD lint — claimed "Steiger (FSD-official) or `eslint-plugin-boundaries`"

- **Verified:** **Steiger** is the official Feature-Sliced Design linter (`feature-sliced/steiger`), published on npm, actively developed — **but still labelled beta** ("APIs may change") and it is a **standalone linter with its own config/CLI, not an ESLint plugin/rule.** `eslint-plugin-boundaries` is a real, maintained ESLint plugin that can express the layer graph. Also in the space: `eslint-plugin-project-structure` (FSD recipe in FSD docs), `eslint-plugin-import-fsd`.
- **Verdict:** 🟠 Both named tools exist, but there is an **internal inconsistency**: AD-2 and AD-16 repeatedly say the frontend rule is "an **ESLint** FSD layer-import rule … build-breaking (AD-2)". Steiger is *not* ESLint — adopting it means a second linter binary in CI, not an ESLint rule. Pick one lane and make the ADs match: either (a) `eslint-plugin-boundaries` (stays within the "ESLint rule" framing the ADs assume), or (b) Steiger as a separate CI gate (then reword AD-2/AD-16 to stop calling it an ESLint rule). Given Steiger's beta status and the ADs' wording, `eslint-plugin-boundaries` is the lower-risk default for v1.
- Sources: https://github.com/feature-sliced/steiger , https://feature-sliced.design/blog/mastering-eslint-config , https://www.npmjs.com/package/eslint-plugin-boundaries

### Container runtime / CI — claimed "Docker + Docker Compose; GitHub Actions"

- **Verified:** Standard, current, no licensing change relevant at this scale.
- **Verdict:** ✅ Fine. (Aside: `docker compose` v2 plugin syntax is the norm; "Docker Compose" as a concept is current.)

---

## Pressure-test answers

### (a) Is "framework-light auth" — cookie auth + `Microsoft.AspNetCore.Identity.PasswordHasher` only, no full Identity — sound for 2026?

**Yes, it is a sound and officially-supported pattern — with one concrete parameter the spine must pin.**

- Cookie authentication without ASP.NET Core Identity is a first-class, documented Microsoft scenario (`AddAuthentication().AddCookie(...)`, `HttpContext.SignInAsync` with a hand-built `ClaimsPrincipal`). It is the right amount of framework for two tiny bespoke account tables and avoids dragging in the Identity EF schema, UI, and `UserManager`/`SignInManager` surface the project doesn't want. AD-13's shape (`HttpOnly; Secure; SameSite=Lax`, `accountType` claim, antiforgery on state-changing requests, persisted Data Protection keys) is correct and complete for v1.
- `Microsoft.AspNetCore.Identity.PasswordHasher<T>` is explicitly recommended for exactly this "I just need to hash passwords" case over hand-rolling `KeyDerivation.Pbkdf2`. It produces a self-describing, versioned hash blob and supports `PasswordVerificationResult.SuccessRehashNeeded` for future algorithm upgrades — good.
- **The gap:** the default `PasswordHasherOptions.IterationCount` for the IdentityV3 (PBKDF2-HMAC-SHA256) format is **100,000** (historically 10,000; raised in later releases) — still **below OWASP's current guidance** (PBKDF2-HMAC-SHA256 ≈ 600,000 iterations; PBKDF2-HMAC-SHA512 ≈ 210,000). OWASP now ranks Argon2id first, then scrypt/bcrypt, with PBKDF2 kept mainly for FIPS. For NexusJob's threat model (portfolio project, no regulated data) PBKDF2 via `PasswordHasher` is acceptable **provided AD-13 pins an explicit `IterationCount` at or above the OWASP number** rather than taking the default. Optionally note Argon2id (via a vetted library such as `Konscious.Security.Cryptography` / `NetDevPack.Security.PasswordHasher`) as the upgrade path — the `PasswordHasher` abstraction makes swapping later cheap.
- **Recommendation:** keep the approach; add to AD-13: "`PasswordHasherOptions.IterationCount` is set explicitly to the current OWASP figure (not the framework default); the hasher abstraction is retained so Argon2id can replace PBKDF2 without a data migration beyond rehash-on-login."
- Sources: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie , https://www.scottbrady.io/aspnet-identity/improving-the-aspnet-core-identity-password-hasher , https://benjamin-abt.com/blog/2026/08/10/custom-password-hashing-aspnet-core/ , OWASP Password Storage Cheat Sheet (https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)

### (b) Is NSwag still the sensible OpenAPI→TS choice for a React SPA, or has the ecosystem moved?

**Partly moved. NSwag is still viable but is no longer the obvious default for the *frontend* client, and it has a live compatibility problem with .NET 10's default output.**

- NSwag remains the strongest option when you also want a **C#** client or server-side artifacts from the same toolchain, and it is maintained (14.7.x, .NET 10 support landed late 2025).
- For a **TypeScript React SPA specifically**, the ecosystem has produced better-fit tools since NSwag's heyday: **Orval** (generates TanStack Query v5 hooks + types + MSW mocks from one config — removes boilerplate AD-15/AD-16 would otherwise hand-write), **Hey API `@hey-api/openapi-ts`** (modern default, plugin-based, optional runtime validation), **openapi-typescript + openapi-fetch** (types-only, zero-runtime, maximal control). All three consume **OpenAPI 3.1** natively.
- The decisive factor here is **Finding H-1**: .NET 10 emits 3.1 by default and NSwag's TS generator has known 3.1 breakage. So the choice is: (i) stay on NSwag **and** force the host to emit 3.0 (`options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0` in the `AddOpenApi` callback), or (ii) move the client generator to Orval / Hey API / openapi-typescript and let the host emit its native 3.1.
- **Recommendation:** given the spine's own stated goals (CRUD-plain, minimal ceremony, React + TanStack Query, generated client is the *only* client-side API description), **Orval is the better 2026 default** for this project — it targets exactly this stack and emits the TanStack Query layer AD-16 wants. Keep NSwag only if a C# client is also on the roadmap. Either way, AD-15 should name the chosen OpenAPI **version** the host emits, not just the generator.
- Sources: https://orval.dev/ , https://heyapi.dev/ , https://openapi-ts.dev/ , https://www.pkgpulse.com/guides/orval-vs-openapi-typescript-vs-kubb-openapi-client-2026 , https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0

### (c) Any .NET 10 feature the spine leans on that isn't real?

**No fabricated features found.** Everything .NET-10-specific the spine relies on checks out:

- **Built-in Minimal API validation** (`AddValidation()`) — real, shipped in .NET 10 (see above). Covers input validation. Only caveat is DataAnnotations expressiveness, which the spine already hedges.
- **Built-in single OpenAPI document** (`Microsoft.AspNetCore.OpenApi`, `MapOpenApi()`) — real; Swashbuckle is out of the templates. The spine's assumption that "the host emits a single OpenAPI document" is correct. The only issue is the **3.1-by-default** behaviour it doesn't mention (Finding H-1) — that's an omission, not a non-existent feature.
- **Cookie auth, Data Protection key persistence, antiforgery/`IAntiforgery`, `ProblemDetails` / `IProblemDetailsService`, RFC 9457** — all long-standing and current in ASP.NET Core 10.
- **UUID v7 generated in application code** — `Guid.CreateVersion7()` exists since .NET 9, present in .NET 10. Real. (PG 18 also offers `uuidv7()` server-side if desired.)
- **EF Core 10 schema-per-`DbContext`, per-module migration history** — standard EF capability, not version-gated.

---

## Tiered findings

### 🔴 Critical
*(none — no committed technology is discontinued, renamed, relicensed, or non-existent)*

### 🟠 High

- **H-1 — NSwag ⇄ .NET 10 OpenAPI 3.1 incompatibility is unaddressed, and AD-15 hard-depends on this pipeline.** .NET 10's built-in OpenAPI generator emits OpenAPI 3.1 (JSON Schema 2020-12) by default; NSwag's TypeScript client generator has known breakage against 3.1 documents. AD-15 makes the generated client "the only permitted description of the API on the client side," so a broken generator blocks frontend work. **Fix:** either pin the host to emit 3.0 (`OpenApiVersion = OpenApiSpecVersion.OpenApi3_0`) and keep NSwag, or switch the generator to a 3.1-native tool (Orval recommended for this stack — it also emits the TanStack Query hooks AD-16 wants). AD-15 must state the emitted OpenAPI **version**, not only the generator name.

- **H-2 — Stack table was not actually reality-checked on 2026-09-05 as the spine header claims.** Three entries are a full major version stale: **TypeScript "5.x"** (7.0 GA 2026-07; 6.x is the conservative current choice), **React Router "v7"** (v8 GA 2026-06; v7 is security-only), **PostgreSQL "17"** (18 GA 2025-09; 19 imminent). None is broken, but the "verified current on the web 2026-09-05" claim is not defensible for these rows. Re-verify and either bump or annotate each with a deliberate-choice rationale.

### 🟡 Medium

- **M-1 — FSD-lint / ESLint inconsistency.** AD-2 and AD-16 describe the frontend boundary check as "an ESLint FSD layer-import rule" that is build-breaking, but the Stack table's first option, **Steiger, is a standalone linter, not an ESLint rule** (and is still beta). Choose one lane: `eslint-plugin-boundaries` (keeps the ADs' "ESLint rule" wording valid — recommended for v1) *or* Steiger as a distinct CI gate (then reword AD-2/AD-16). 

- **M-2 — AD-13 relies on `PasswordHasher` defaults.** The approach is sound (see pressure-test a) but AD-13 must **explicitly pin `PasswordHasherOptions.IterationCount`** to the current OWASP figure rather than accept the framework default, and should name Argon2id as the sanctioned upgrade path.

- **M-3 — "Built-in .NET 10 minimal-API validation" is DataAnnotations-only.** It exists and covers input validation, but the Stack note / AD-3 should state the limitation (no cross-field/async rule DSL) so the deferral of FluentValidation is a documented trade-off, not a surprise.

### 🟢 Low / Informational

- **L-1 — ArchUnitNET is still pre-1.0 (0.13.3).** Correct pick, actively maintained, NetArchTest-rejection rationale is accurate. Pin the exact version in `NexusJob.ArchitectureTests` and expect occasional fluent-API changes on minor bumps.

- **L-2 — PostgreSQL 18 offers `uuidv7()` server-side** and a large read-throughput win. Not required (app-side UUID v7 is fine per the spine), but a reason to prefer 18 over 17 for a greenfield DB.

- **L-3 — Vite 8 is a large internal change (Rolldown/Rust).** Version is current and correct; just pin exactly and confirm any generator/test Vite plugins (and React Router v8's Vite Environment API requirement) are satisfied — they are, on Vite 8.

- **L-4 — NSwag deferred-item in the spine ("re-check NSwag's current release/known-issues state") is good practice** and already present; H-1 is the concrete result of doing that check now.

- **L-5 — TanStack Query "v5" is correct for React** (v6 is Svelte/Solid-only). No action. Good example of a row that *was* checked properly.

---

## Summary table

| Technology | Claimed | Verified state (2026-09-05) | Verdict |
| --- | --- | --- | --- |
| .NET / C# | 10 LTS / 14 | .NET 10 GA 2025-11-11, LTS; C# 14 | ✅ current |
| ASP.NET Core Minimal APIs | 10 | Current; OpenAPI now 3.1-by-default | ✅ (see H-1) |
| EF Core + Npgsql | 10 | EF Core 10 GA; Npgsql EF provider 10.0.x | ✅ current |
| PostgreSQL | 17 | 18 GA 2025-09-25; 17 supported to 2029 | 🟡 one major behind |
| ArchUnitNET | current | 0.13.3 (2026-03), actively maintained | ✅ correct pick |
| Minimal-API validation | built-in .NET 10 | Real (`AddValidation()`), DataAnnotations-based | ✅ exists (M-3 caveat) |
| Testcontainers for .NET | current | 4.14.0 (2026-08), active | ✅ current |
| React | 19.2.x | 19.2 is latest stable | ✅ current |
| TypeScript | 5.x | 7.0 GA 2026-07-08; 6.0 prior | 🟡 two majors behind |
| Vite | 8.x | 8.2.x, GA 2026-03-12 (Rolldown) | ✅ current |
| TanStack Query | v5 | React package still v5 | ✅ current |
| React Router | v7 | v8 GA 2026-06; v7 security-only | 🟡 one major behind |
| OpenAPI→TS generator | NSwag | Maintained (14.7.1); **3.1 breakage** vs .NET 10 default | 🟠 H-1 |
| FSD lint | Steiger / eslint-plugin-boundaries | Steiger real but beta & not-ESLint; plugin real | 🟠 M-1 |
| Framework-light auth | cookie + PasswordHasher | Officially supported pattern; pin iteration count | 🟠 M-2 |
| Docker / Compose / GitHub Actions | — | Current | ✅ |
