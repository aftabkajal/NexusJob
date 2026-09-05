# Dimension: patterns — Round 1

## Findings

- claim: For .NET projects in 2025, the emerging consensus recommendation for new projects is Clean Architecture combined with Vertical Slices as a hybrid — layered structure for complex shared domain logic, vertical slices for per-feature organization — rather than picking one pattern exclusively.
  source: https://antondevtips.com/blog/n-layered-vs-clean-vs-vertical-slice-architecture
  publisher: antondevtips.com (Anton Martyniuk, .NET practitioner blog)
  pub_date: 2025 (exact month undated)
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://milanjovanovic.tech/blog/vertical-slice-vs-clean-architecture

- claim: Milan Jovanović (high-profile .NET educator) advises Vertical Slice Architecture for CRUD-heavy APIs, small teams, and fast feature delivery, and Clean Architecture for complex shared domain logic and large long-lived teams — and describes combining vertical slices *within* a Clean Architecture project structure ("Pragmatic Clean Architecture") as his own preferred hybrid. DDD/CQRS concepts are referenced but MediatR is not named in this specific article.
  source: https://milanjovanovic.tech/blog/vertical-slice-vs-clean-architecture
  publisher: milanjovanovic.tech (Milan Jovanović)
  pub_date: 2026-08 (dated Aug 13, 2026)
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://antondevtips.com/blog/n-layered-vs-clean-vs-vertical-slice-architecture

- claim: Vertical Slice Architecture was popularized in .NET by Jimmy Bogard (creator of AutoMapper and MediatR) starting around 2018, out of frustration with horizontally-layered architectures; MediatR is the tool he built to facilitate implementing vertical slices with CQRS-style command/query handlers and a pipeline for cross-cutting concerns.
  source: https://davidgiard.com/jimmy-bogard-on-vertical-slice-architecture-and-mediatr
  publisher: davidgiard.com (David Giard, MVP/podcast host, summarizing a direct interview with Bogard)
  pub_date: undated (references a 2018-era origin claim)
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://dev.to/htech/exploring-vertical-slices-in-dotnet-core-3mik

- claim: A Modular Monolith divides an application into modules that each own a bounded context, giving most of DDD's structural benefit without microservices' operational cost; it is described as an easier stepping-stone toward microservices later (since the module boundaries already exist) than retrofitting boundaries onto a non-modular monolith.
  source: https://medium.com/@curiousraj/stop-overcomplicating-ddd-why-modular-monoliths-are-the-smarter-choice-c887e3850fe8
  publisher: Medium (Rajnish Kumar)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://mehmetozkaya.medium.com/comparing-monolith-microservices-and-modular-monoliths-communications-data-development-and-5ebf643191fd

- claim: A named .NET practitioner (Jon P Smith, author of an EF Core book) reports first-hand production experience: modular monolith (22 projects, one per bounded-context feature) was "brilliant" and he'd use it again — it reduced complexity and made refactoring safer — but required real design effort around project naming/dependency rules, and under a real deadline (finishing his EF Core book by Nov 2020) he admits he violated his own architectural module-boundary rules to hit the deadline, creating unwanted cross-module dependencies. DDD tactical patterns (rich entities with meaningful methods) protected invariants but required substantially more code, cross-entity business logic remained awkward, and strict entity-method DDD was too restrictive for some client/frontend patterns (e.g., JSON Patch), leading him to adopt a "hybrid DDD" compromise that risks bypassing invariant protection.
  source: https://www.thereformedprogrammer.net/my-experience-of-using-modular-monolith-and-ddd-architectures/
  publisher: thereformedprogrammer.net (Jon P Smith)
  pub_date: 2021-02 (last updated 2021-07)
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: none found (this is a first-person practitioner account; treat as a single strong anecdote, not yet a verified trend)

- claim: Feature-Sliced Design (FSD) is a named, documented frontend architecture methodology for React (and framework-agnostic) apps organizing code by feature/business-area slices (e.g., user, product, cart) rather than by technical layer, explicitly described by its own docs as creating natural bounded contexts in the frontend, each independently testable.
  source: https://feature-sliced.design/docs
  publisher: feature-sliced.design (official FSD documentation site)
  pub_date: undated (living docs site)
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://serhiikoziy.medium.com/feature-sliced-design-architecture-in-react-with-typescript-447dc5e6a411

- claim: Micro-frontends with Webpack/Vite Module Federation are explicitly framed by practitioner blogs as a solution for team-scale coordination problems (merge conflicts, release coordination across teams on a monolithic frontend) rather than a default architecture, with warnings that initial Module Federation config is the easy part and real difficulty appears post-launch in production.
  source: https://www.bitovi.com/blog/should-your-team-be-using-micro-frontends-and-module-federation
  publisher: bitovi.com (Bitovi, frontend consultancy)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://www.tothenew.com/blog/micro-frontends-with-module-federation-is-it-actually-worth-the-complexity/ (title/summary retrieved via search snippet only; full-page fetch returned HTTP 403, so treat with reduced confidence pending direct access)

- claim: Splitting microservices by data entity (User, Product, Order services) rather than by true bounded context is a commonly cited mistake that produces a "distributed monolith" — services that remain tightly coupled with the coupling simply spread across network boundaries.
  source: https://isharadbharadwaj.medium.com/the-architects-blueprint-using-domain-driven-design-ddd-and-bounded-contexts-to-define-73b5a265b28c
  publisher: Medium (Sharad Bharadwaj)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://www.cerbos.dev/blog/determining-service-boundaries-and-decomposing-monolith

- claim: A foundational and still-widely-cited counterintuitive claim: "a Bounded Context is the exact opposite of a Microservice" — DDD bounded contexts define the *largest* valid service boundary (no conflicting models inside), while microservices typically require decomposing further within a bounded context; not every bounded context should become one microservice, and using "one bounded context = one microservice" as a rule of thumb is flawed.
  source: https://vladikk.com/2018/01/21/bounded-contexts-vs-microservices/
  publisher: vladikk.com (Vlad Khononov, author of "Learning Domain-Driven Design")
  pub_date: 2018-01
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://blog.nashtechglobal.com/bounded-context-in-microservice/ (independently repeats the same distinction; original source is 2018, exceeding the 2-year pattern freshness bar on its own, but the claim is corroborated by newer, undated practitioner posts making the same point, suggesting it remains current conventional wisdom rather than stale)

- claim: Nx enforces monorepo module boundaries via a tag-based system (project.json tags + the @nx/enforce-module-boundaries ESLint rule) that can encode bounded-context-style dependency rules (e.g., feature modules cannot reach into unrelated domains' internals); Turborepo only introduced an experimental "Boundaries" feature (tags, default boundaries) starting in v2.4.2, and teams migrating from Nx to Turborepo have flagged the loss of Nx's stricter guardrails against circular dependencies as a concern.
  source: https://dev.to/sakthicodes22/stop-the-spaghetti-enforcing-module-boundaries-in-an-nx-monorepo-2a24
  publisher: DEV Community (sakthicodes22)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://github.com/vercel/turborepo/discussions/9435 (Turborepo's own RFC discussion confirming Boundaries is a newer/less mature feature)

- claim: Practitioner consensus (multiple 2025-era .NET blogs) explicitly warns against applying full Clean Architecture (MediatR + generic repository + Unit of Work + AutoMapper + specification pattern) to simple CRUD apps, prototypes, spikes, or tiny solo projects, calling the added indirection "expensive" relative to the thin business logic it protects; recommendation is to start simple (a single project, or Vertical Slice) and only layer in Clean Architecture abstractions once domain complexity, project longevity, and multi-developer teams justify the cost.
  source: https://medium.com/@gunjanmodi/is-clean-architecture-overengineering-ccca6ff34dcc
  publisher: Medium (Gunjan Modi)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://dev.to/syawqy/clean-architecture-in-net-when-to-use-it-and-how-to-stay-flexible-5fpk

## Leads for next round

- Verify MediatR/AutoMapper commercial-license change (announced 2025) and its practical effect on the "CQRS + MediatR" default recommendation for new .NET projects.
- Chase a working (non-403) copy of the TO THE NEW micro-frontend article, or an equivalent full-text production postmortem with named company/timeline, to properly evidence the "micro-frontend regret" claim as a two-source trend rather than a search-snippet summary.
- Look for a second independent account of "vertical slice sprawl" (duplication / shared-kernel sprawl / behavior-sprawl from stacked pipeline behaviors) to upgrade it from single-source to trend.
- Check whether Ardalis's "Clean Architecture" solution template (a widely cited reference implementation) has current guidance reconciling Clean Architecture with Vertical Slices/DDD, and its recency.
- Look for an academic/systematic-literature-review source on DDD implementation challenges (arxiv 2310.01905 surfaced but not yet read) to cross-check practitioner claims against a more rigorous synthesis.

## Searched for but could not find

- A full-text, non-paywalled, non-403 production postmortem naming a specific company's micro-frontend rollout with concrete numbers/timeline (only aggregator-style summaries were retrievable this round).
- Direct evidence of "vertical slice sprawl" as a documented failure in a named production system (found only general practitioner warnings, not a concrete postmortem).

## Round stop reason
Round cap reached for round 1's planned scope (4 broad + 4 follow-up queries = 8 queries covering all three sub-topics); most load-bearing claims have credible sourcing. Proceeding to round 2 to chase the specific leads above (MediatR commercialization, micro-frontend postmortem verification, vertical-slice-sprawl second source, bounded-context/microservice claim recency check).
