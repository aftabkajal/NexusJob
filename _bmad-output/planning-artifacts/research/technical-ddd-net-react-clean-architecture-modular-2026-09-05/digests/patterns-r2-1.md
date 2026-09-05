# Dimension: patterns — Round 2

## Findings

- claim: On April 2, 2025, Jimmy Bogard announced that AutoMapper and MediatR (the two libraries most associated with implementing CQRS/Vertical Slice Architecture in .NET) are moving to a commercial licensing model under his new company, Lucky Penny Software, with the commercial edition launching July 2, 2025. Motivation given: after losing corporate sponsorship (Headspring) roughly five years earlier, he had insufficient time to maintain widely-used OSS projects without funding. Existing pre-transition versions remain MIT-licensed and archived; new development happens under the commercial license.
  source: https://milanjovanovic.tech/blog/mediatr-and-masstransit-going-commercial-what-this-means-for-you
  publisher: milanjovanovic.tech (Milan Jovanović)
  pub_date: 2025-04
  accessed: 2026-09-05
  confidence: high
  class: ecosystem-signal
  independent_second_source: https://emreteoman.medium.com/automapper-and-mediatr-going-commercial-what-happens-next-9e62b46cee5f

- claim: This licensing change is prompting .NET practitioners to publish "build your own mediator" guides as an alternative to adopting MediatR's commercial license, indicating a live, ongoing ecosystem reaction (not a one-off announcement) that architects choosing "CQRS + MediatR" as a default pattern for new 2026 projects should factor licensing cost/lock-in into that decision, or consider a lightweight in-house pipeline instead.
  source: https://medium.com/@prateektiwari14/mediatr-going-commercial-heres-how-to-build-your-own-mediator-in-net-043310a9c876
  publisher: Medium (Prateek Tiwari)
  pub_date: 2025 (undated month, post-April 2025)
  accessed: 2026-09-05
  confidence: medium
  class: ecosystem-signal
  independent_second_source: https://www.jimmybogard.com/tag/mediatr/ (Bogard's own blog confirms the transition timeline from the source side)

- claim: Ardalis's official Clean Architecture reference templates (widely used as a canonical .NET Clean Architecture starting point) now explicitly offer two variants: a full multi-project Clean Architecture template (Core/UseCases/Infrastructure/Web) and a "Minimal Clean Architecture" template that is single-project and organized by vertical slices — explicitly positioned for MVPs and smaller apps, with a documented migration path from minimal/vertical-slice to full Clean Architecture as complexity grows. Ardalis also maintains a dedicated repo ("VerticalCleanModularMicroservices") comparing and evolving .NET architecture from Vertical Slices through Clean Architecture, Modular Monoliths, to Microservices using EF Core, Aspire, and a Mediator pattern.
  source: https://ardalis.github.io/CleanArchitecture/getting-started/
  publisher: ardalis.github.io (Steve "Ardalis" Smith, well-known .NET Clean Architecture author/consultant)
  pub_date: undated (as of .NET 9/10-era docs, referencing dotNetConf 2025 and ASP.NET Core 10)
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://github.com/ardalis/VerticalCleanModularMicroservices

- claim: Real-world Module Federation micro-frontend deployments from the 2021-2023 era are widely described (retrospectively) as messy in production: shared state described as "a nightmare," CSS leaking across app boundaries, ballooning bundle sizes, and the promised "independent deployments" benefit breaking down whenever two teams needed the same version of a shared dependency like React; release coordination across teams reportedly degenerated into frequent cross-team Slack threads and daily merge-conflict friction. Tooling for observability/debugging across federated module boundaries is also described as still immature.
  source: https://dev.to/bitdev_/module-federation-building-a-micro-frontends-solution-in-2024-1jm0
  publisher: DEV Community (bitdev_ / Bit)
  pub_date: 2024
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://www.bitovi.com/blog/should-your-team-be-using-micro-frontends-and-module-federation (independently frames micro-frontends as a response to real monolith-at-scale pain rather than a default choice, corroborating that the pattern carries real integration cost)

- claim: A second, independent practitioner source corroborates the "vertical slice sprawl" failure mode identified in round 1: common anti-patterns named are "fake slices," "shared-kernel sprawl," and "over-abstraction," alongside a concrete rule of thumb — don't extract a shared abstraction until three real usages exist with identical, stable logic, since "duplication is cheaper than the wrong abstraction." A related and distinct sprawl mode is "behavior sprawl" from stacking many MediatR pipeline behaviors (cross-cutting concerns) onto every request, making a 10-behaviors-deep request pipeline hard to debug.
  source: https://milanjovanovic.tech/blog/cross-cutting-concerns-in-vertical-slice-architecture
  publisher: milanjovanovic.tech (Milan Jovanović)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://antondevtips.com/blog/how-to-avoid-code-duplication-in-vertical-slice-architecture-in-dotnet (independently discusses the same duplication trade-off and remediation guidance for Vertical Slice Architecture)

- claim: The "bounded context is not the same as (and is typically larger than) a microservice" claim, originally made by Vlad Khononov in 2018, is independently repeated as current guidance by unrelated, more recent practitioner sources: not every bounded context should map 1:1 to a microservice; sometimes a bounded context is too large and must be split into multiple services, and sometimes multiple small bounded contexts should be combined into one service — the decision should weigh non-functional factors (scalability needs, data storage, deployment frequency, team ownership, communication patterns), not just domain-model boundaries alone.
  source: https://blog.nashtechglobal.com/bounded-context-in-microservice/
  publisher: NashTech Blog (Nashtech, a software consultancy)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://vladikk.com/2018/01/21/bounded-contexts-vs-microservices/ (original 2018 source; the claim's persistence across ~7 years of independent restatement supports treating it as durable conventional wisdom rather than stale, despite the original falling outside the strict 2-year freshness bar)

- claim: An arXiv systematic literature review on DDD implementation (Özkan, Babur, van den Brand) exists and catalogs implementation challenges around bounded contexts, context mapping, aggregates, and DDD/microservices integration difficulties, following Kitchenham/Wohlin systematic-review methodology across 80+ cited works — but the specific quantitative findings and named failure modes could not be reliably extracted from this session's automated PDF fetch (content came back as a generic structural summary rather than specific sourced claims). Treat as a promising secondary-source lead for a future, deeper read rather than as an evidenced finding this round.
  source: https://arxiv.org/pdf/2310.01905
  publisher: arXiv preprint (Özkan, Babur, van den Brand)
  pub_date: 2023-10 (v4 updated 2025-06)
  accessed: 2026-09-05
  confidence: low
  class: pattern
  independent_second_source: none found — flagging as unverified/needs direct reading, not citing any specific claim from it in the summary above

## Leads for next round

- Directly read (not auto-summarize) the arXiv 2310.01905 PDF to extract specific, citable findings if this research is revisited.
- Get full-text access to the TO THE NEW micro-frontend article (blocked by 403 both rounds) or find an alternative primary source with a named company case study and concrete metrics/timeline for micro-frontend regret.
- If MediatR licensing cost becomes decision-relevant, check Lucky Penny Software's current published pricing/terms directly (not found this session).

## Searched for but could not find

- A named-company, metrics-backed micro-frontend production postmortem (only aggregator/consultancy blog summaries were retrievable).
- Direct extraction of specific findings from the arXiv DDD systematic literature review (fetch returned only structural/generic summary).

## Round stop reason
Coverage: all three sub-dimensions (backend .NET patterns, frontend React modular patterns, and postmortem/regret evidence) now have at least one high-or-medium-confidence finding with an independent second source, plus a live, verified ecosystem signal (MediatR commercialization) directly relevant to the CQRS+MediatR recommendation. A third round would mostly re-surface the same aggregator content already seen (novelty was already diminishing on the arXiv/micro-frontend-postmortem leads), so stopping at 2 rounds per budget guidance.
