# Dimension: ecosystem — Round 1

## Findings
- claim: MediatR moved to a dual commercial/open-source license starting at v13.0.0 (published 2025-07-02), requiring a license key (`cfg.LicenseKey`) for commercial use; the project is still being actively released, with v14.0.0 (2025-12-03), v14.1.0 (2026-03-03), and v14.2.0 (2026-07-02) confirmed directly on the NuGet package page.
  source: https://www.nuget.org/packages/mediatr/
  publisher: NuGet Gallery (primary package registry — authoritative for version/date)
  pub_date: 2026-07 (latest version's publish date)
  accessed: 2026-09-05
  confidence: high
  class: version
  independent_second_source: https://github.com/LuckyPennySoftware/MediatR/releases

- claim: MediatR (and AutoMapper) ownership/maintenance moved to a new company, Lucky Penny Software, founded by original maintainer Jimmy Bogard, coinciding with the commercial-license transition; Bogard has publicly stated the reason was that his open-source contributions had previously been employer-sponsored, and once that sponsorship ended "contributions cratered and flat-lined," making commercial licensing necessary for sustained maintenance.
  source: https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you
  publisher: Milan Jovanović (.NET practitioner blog, citing Bogard's own statements)
  pub_date: 2025 (per article context; MediatR transition dated 2025-07)
  accessed: 2026-09-05
  confidence: medium
  class: ecosystem-signal
  independent_second_source: https://www.youtube.com/watch?v=Un3GgiDElCA

- claim: MassTransit (popular .NET service-bus/messaging library, often paired with MediatR/DDD-style .NET architectures) is also transitioning to a commercial license with v9: Q3 2025 prerelease for early adopters, Q1 2026 official commercial release; v8 remains Apache 2.0 with security patches continuing through at least end of 2026. Pricing at announcement was $400/mo or $4,000/yr (SMB) and $1,200/mo or $12,000/yr (enterprise), described as not yet final at announcement, with a revenue-based free tier for startups/small orgs.
  source: https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you
  publisher: Milan Jovanović (.NET practitioner blog)
  pub_date: 2025 (announcement timeframe)
  accessed: 2026-09-05
  confidence: medium
  class: ecosystem-signal
  independent_second_source: https://news.ycombinator.com/item?id=43565690

- claim: A fragmented ecosystem of free/OSS MediatR alternatives has emerged in direct response to the commercial shift, most built on C# source generators for AOT-friendliness and zero/low allocation: "Mediator" by martinothamar (source-generator based, Native AOT support), "SwitchMediator" (zero-allocation, API-compatible with MediatR, compile-time errors for missing handlers), "DispatchR", and "Concordia" (source-generator handler discovery).
  source: https://github.com/martinothamar/Mediator
  publisher: GitHub (primary repo)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: ecosystem-signal
  independent_second_source: https://github.com/zachsaw/SwitchMediator

- claim: The Ardalis "CleanArchitecture" template repository shows continued community engagement: ~17k GitHub stars, ~2.9k forks, 26 open issues, 762 commits on the main branch (exact recency of the most recent commit could not be confirmed from the page content retrieved).
  source: https://github.com/ardalis/CleanArchitecture
  publisher: GitHub (primary repo)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: ecosystem-signal
  independent_second_source: none found for the specific star/fork/issue counts — single primary-source snapshot; treat exact numbers as a point-in-time reading, not independently corroborated.

- claim: FastEndpoints (a popular alternative/complement to MVC + MediatR for building vertical-slice-style .NET APIs) is actively maintained, with stable release 8.3.0 published 2026-08-20 and a prerelease 8.4.0-beta.3 published 2026-08-30, confirmed directly via the NuGet package page.
  source: https://www.nuget.org/packages/FastEndpoints/
  publisher: NuGet Gallery (primary registry)
  pub_date: 2026-08
  accessed: 2026-09-05
  confidence: high
  class: version
  independent_second_source: https://github.com/FastEndpoints/FastEndpoints/releases

- claim: Nx (monorepo tool relevant to modular React front-end organization) is under very active development: v23.2.0 released 2026-09-02 (days before this research), following v23.1.3 (2026-08-31) and release candidates through late August/early September 2026, with recent feature work including Angular v22.1 support and an oxlint/oxfmt toolchain integration.
  source: https://github.com/nrwl/nx/releases
  publisher: GitHub (primary repo)
  pub_date: 2026-09-02
  accessed: 2026-09-05
  confidence: high
  class: version
  independent_second_source: none found as a second independent source for this exact release, but it is a primary-source release feed, which is the strongest available evidence class for this claim type.

- claim: Turborepo (JS/TS monorepo build tool, Vercel-owned) is also actively maintained, with a canary release v2.10.13-canary.1 dated 2026-08-26, and Vercel's own documentation (updated 2026-08-11) recommending upgrading off pre-2.4.1 versions due to Skew Protection asset issues.
  source: https://www.gitwatchman.com/track/vercel/turbo
  publisher: GitWatchman (release-tracking aggregator, not primary)
  pub_date: 2026-08-26
  accessed: 2026-09-05
  confidence: medium
  class: version
  independent_second_source: https://vercel.com/docs/monorepos/turborepo

## Leads for next round
- Confirm AutoMapper's own commercial-license transition specifics (version number, license text) as a third leg of the Bogard/Lucky Penny Software commercialization story, since it's frequently mentioned alongside MediatR/MassTransit but wasn't yet independently verified.
- Check Nx's parent company (Nrwl / Narwhal Technologies) funding/financial-durability signals, given Nx's centrality to modular monorepo tooling.
- Check whether Module Federation itself (the OSS tooling, distinct from Zephyr Cloud's commercial deployment layer) shows any maintainer-health concerns.

## Searched for but could not find
- An exact "last commit" timestamp for ardalis/CleanArchitecture (GitHub page content did not surface it via fetch).

## Round stop reason
coverage — the central "five-year regret risk" signal (MediatR/MassTransit/AutoMapper commercialization and the resulting alternative-library fragmentation) was found and corroborated across multiple independent sources; proceeding to round 2 to fill specific gaps (AutoMapper details, Nx company funding) rather than due to lack of results.
