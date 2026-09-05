# Dimension: landscape — Round 1

## Findings
- claim: Jason Taylor's "Clean Architecture Solution Template" remains actively developed, with v10.8.0 released (dependency updates, Angular/React frontends, migration from Create React App to Vite, Pico CSS refresh); it also supports .NET Aspire.
  source: https://jasontaylor.dev/clean-architecture-template-10-8-0-released/
  publisher: Jason Taylor (template author)
  pub_date: undated (referenced as recent 2026 release in secondary coverage)
  accessed: 2026-09-05
  confidence: medium
  class: landscape
  independent_second_source: https://github.com/jasontaylordev/cleanarchitecture

- claim: Ardalis's "Clean Architecture Solution Template" is positioned for ASP.NET Core 10 and remains one of the two dominant Clean-Architecture starter templates alongside Jason Taylor's.
  source: https://github.com/ardalis/cleanarchitecture
  publisher: Ardalis (Steve Smith) / GitHub
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: landscape
  independent_second_source: https://jasontaylor.dev/clean-architecture-template-10-8-0-released/

- claim: Vertical Slice Architecture and DDD are increasingly framed as complementary, not competing: DDD models the business (bounded contexts, aggregates), Vertical Slice organizes the application around behavior/features; many .NET teams build feature slices that enforce Clean Architecture boundaries internally rather than picking one pattern exclusively.
  source: https://fullstackcity.com/ddd-and-vertical-slice-architecture-are-friends-not-rivals
  publisher: fullstackcity.com (practitioner blog)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://ricofritzsche.me/why-vertical-slices-wont-evolve-from-clean-architecture/

- claim: Feature-Sliced Design (FSD) is described by multiple 2026 sources as the most widely adopted React architecture standard for organizing large front-end codebases, using a layered structure (app/pages/widgets/features/entities/shared) with slices divided into segments (ui/api/model/lib/config).
  source: https://feature-sliced.design/docs
  publisher: Feature-Sliced Design official docs
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://softaims.com/blog/scalable-react-architecture-patterns-2026

- claim: The framing "Module Federation vs. monorepo" is considered a false dichotomy by practitioners in 2026 — the real comparison is Module Federation vs. other composition/ownership patterns; a well-structured monorepo with enforced package boundaries often solves the underlying problem (team ownership, independent iteration) with much less operational complexity than introducing runtime remotes.
  source: https://medium.com/@balajibal/module-federation-in-react-what-it-solves-where-it-hurts-and-how-it-compares-be3e5601036a
  publisher: Medium (practitioner author)
  pub_date: 2026-06 (per article dateline "Jun, 2026")
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://dotpingdesign.com/micro-frontends-2026-module-federation-multi-zones/

- claim: In 2026, Next.js "Multi-Zones" (routing/asset-prefix-layer composition of independently built Next.js apps under one domain) and Module Federation (webpack 5 / Rspack ecosystem, runtime code-sharing between bundles) are positioned as solving different problems rather than being direct substitutes; choose Multi-Zones when hard navigations between path groups are acceptable, Module Federation when shared libraries/lazy remotes must load without republishing an npm major.
  source: https://dotpingdesign.com/micro-frontends-2026-module-federation-multi-zones/
  publisher: DotpingDesign (practitioner/agency blog)
  pub_date: 2026 (undated within year)
  accessed: 2026-09-05
  confidence: low
  class: pattern
  independent_second_source: https://nextjs.org/docs/app/guides/multi-zones

- claim: The modular monolith has become the pragmatic default recommendation for .NET enterprise teams in 2026, positioned as a middle ground giving bounded-context-style module boundaries and event-driven internal communication without full distributed-systems complexity; recommendation is to move to microservices only once scaling/organizational limits are actually hit.
  source: https://milanjovanovic.tech/blog/modular-monolith-architecture-dotnet
  publisher: Milan Jovanović (.NET practitioner/content creator)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: landscape
  independent_second_source: https://abp.io/architecture/modular-monolith

- claim: .NET Aspire has become the primary/first-class tool cited in 2026 sources for building and orchestrating .NET modular monoliths and microservices (local orchestration, service discovery, OpenTelemetry-based observability), and is promoted as an incremental path from monolith toward microservices.
  source: https://aspiresoftwareconsultancy.com/dotnet-aspire-modular-monolith/
  publisher: Aspire Software Consultancy (vendor blog — marketing register, treat with caution)
  pub_date: 2026 (undated within year)
  accessed: 2026-09-05
  confidence: low
  class: landscape
  independent_second_source: https://www.cigen.io/insights/from-monolith-to-microservices-with-net-aspire-a-modernization-roadmap-for-net-apps

- claim: On monorepo tooling for JS/TS (relevant to modular React front ends), the 2026 consensus split is: Turborepo optimizes for JS/TS teams wanting speed without ceremony (typically 5-50 packages), Nx optimizes for platform teams wanting code generation, architectural-boundary enforcement, and polyglot support; common advice is "start with Turborepo, graduate to Nx when coordination becomes the bottleneck."
  source: https://theartofcto.com/technologies/compare/nx/turborepo
  publisher: The Art of CTO (comparison/analyst site)
  pub_date: 2026 (undated within year)
  confidence: medium
  accessed: 2026-09-05
  class: pattern
  independent_second_source: https://daily.dev/blog/monorepo-turborepo-vs-nx-vs-bazel-modern-development-teams/

- claim: A cited adoption figure — "roughly 63% of companies with 50+ developers now run monorepos" — appears in 2026 comparison content but is unsourced/unattributed to a named survey.
  source: https://daily.dev/blog/monorepo-turborepo-vs-nx-vs-bazel-modern-development-teams/
  publisher: daily.dev (aggregator/content site)
  pub_date: 2026 (undated)
  accessed: 2026-09-05
  confidence: low
  class: landscape
  independent_second_source: none found — flagged as unverified statistic, do not treat as fact

## Leads for next round
- Check .NET Aspire's actual production-adoption evidence (named companies, not vendor marketing) and its GA/versioning status.
- Check Next.js Multi-Zones maturity/App Router gap claims with a primary Next.js source.
- Look for named production case studies of Feature-Sliced Design at scale (the "most widely adopted" claim is asserted repeatedly but not evidenced with named companies).

## Searched for but could not find
- A named, production-scale case study (with numbers/timeline) of Feature-Sliced Design adoption — all sources found are methodology explainers or advocacy blogs, not retrospectives.
- Independent verification of the "63% of 50+-dev companies run monorepos" statistic.

## Round stop reason
coverage — core landscape questions for both .NET/DDD and React/modular sides were answered with at least medium confidence; proceeding to round 2 to chase specific leads (Aspire adoption evidence, Next.js Multi-Zones specifics) rather than because of novelty exhaustion.
