# Dimension: implementation — Round 1

## Findings
- claim: Teams new to Clean Architecture in .NET commonly report an initial reaction that it's unnecessary complexity ("just more folders and interfaces for no reason"), but come to see value after living with it on a real project; practitioner advice is to start small (pick one feature to structure this way first) rather than restructuring an entire codebase up front.
  source: https://dev.to/mrodriguesweb/what-i-learned-building-my-first-clean-architecture-project-in-net-4mff
  publisher: DEV Community (first-person practitioner account)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://dev.to/gramli/clean-architecture-in-net-real-world-pros-cons-and-trade-offs-3m9i

- claim: A recurring practitioner criticism of Jason Taylor's widely-used "CleanArchitecture" ASP.NET Core solution template is that its Application layer depends directly on Entity Framework Core (via DbContext/DbSet as unit-of-work/repository), which conflicts with Uncle Bob's strict Clean Architecture rule that the application layer must not depend on the persistence framework; Taylor's own defense is that DbContext-as-unit-of-work makes additional repository abstraction unnecessary.
  source: https://github.com/jasontaylordev/CleanArchitecture/discussions/482
  publisher: GitHub Discussions (template author's own repo, primary)
  pub_date: undated
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://blog.ndepend.com/clean-architecture-for-asp-net-core-solution/

- claim: DDD practitioners report that teams attempting tactical patterns (entities, value objects, aggregates, repositories, domain events) without first investing in the strategic side (bounded contexts, subdomain division) tend not to get results — sequencing (strategic understanding before tactical implementation) is cited as the key adoption lesson, alongside the general point that DDD's learning curve is steep and works best in long-term, iterative, high-complexity projects with realistic expectations about upfront cost vs. long-term payoff.
  source: https://learn.microsoft.com/en-us/azure/architecture/microservices/model/tactical-domain-driven-design
  publisher: Microsoft Learn (Azure Architecture Center)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://medium.com/spraja08/domain-driven-design-event-storming-a-practitioners-guide-to-overcoming-misconceptions-e941419942ac

- claim: In .NET modular monoliths, boundary enforcement in practice relies on tooling rather than developer discipline alone: module "Contracts" projects act as the only public surface (compiler blocks cross-module type access when there's no project reference), and architecture-testing tools (NetArchTest, ArchUnitNET) plus static-analysis tools (NDepend CQLinq) are used in CI/PR gates to fail builds on namespace-level boundary violations or dependency cycles the compiler alone can't catch.
  source: https://dev.to/aloknecessary/enforcing-modular-monolith-boundaries-in-net-ndepend-parallel-pipelines-and-the-architecture-37e1
  publisher: DEV Community (practitioner account)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://fullstackcity.com/part-1-enforcing-true-module-boundaries-in-a-net-modular-monolith

- claim: Practitioner sources note that as a modular monolith grows, local developer friction (full local boot time, full test-suite run time) can become "several minutes," a real operational cost cited as a trade-off consideration — but no source in this round gave hard, dated numbers (e.g., specific minutes at specific module/LOC counts).
  source: https://dev.to/aloknecessary/enforcing-modular-monolith-boundaries-in-net-ndepend-parallel-pipelines-and-the-architecture-37e1
  publisher: DEV Community (practitioner account)
  pub_date: undated
  accessed: 2026-09-05
  confidence: low
  class: pattern
  independent_second_source: none found

- claim: Twilio's Segment engineering team built and operated 140+ microservices for their event-destination pipeline, reached a point where the team was adding ~3 destinations/month but operational overhead grew linearly with each new service, defect rate rose and velocity dropped, and 3 full-time engineers spent most of their time keeping the system alive rather than building product — they collapsed the destinations into a single monolithic service (fronted by an aggregator called "Centrifuge"), after which one engineer could deploy the service in minutes and developer productivity substantially improved.
  source: https://www.twilio.com/en-us/blog/developers/best-practices/goodbye-microservices
  publisher: Twilio (Segment engineering blog, primary, first-person)
  pub_date: 2018 (original Segment post; republished/mirrored on Twilio's blog after Segment acquisition)
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://www.sdxcentral.com/news/segment-struggled-with-microservices-went-back-to-monolith/
  note: this is a 2018 case study, older than the 2-year "pattern" freshness bar; still widely cited in 2026 secondary sources as a canonical example, but no direct evidence found that it reflects Segment's *current* (2026) architecture — flagged as historical, not confirmed-current.

- claim: Amazon Prime Video's Video Quality Analysis / monitoring team migrated their tool from a distributed serverless/microservices architecture (Lambda + Step Functions) to a monolithic application running on EC2/ECS, reporting a 90% reduction in infrastructure cost and improved scaling headroom (able to handle "thousands of streams" with capacity to spare); the account is attributed to Prime Video Senior SDE Marcin Kolny.
  source: https://thenewstack.io/return-of-the-monolith-amazon-dumps-microservices-for-video-monitoring/
  publisher: The New Stack (tech press, citing Amazon's own engineering blog)
  pub_date: 2023 (original Prime Video post; this is a secondary summary)
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://medium.com/@Monika_Sharma1/why-amazon-prime-video-moved-from-serverless-to-monolithic-and-saved-90-costs-660a0d69ac71
  note: NOT a .NET/DDD/React case study — cited only as landscape evidence for the broader "modular monolith over microservices" trend informing implementation-reality expectations; primary Amazon engineering blog post itself was not directly fetched this round.

## Leads for next round
- Verify (or debunk) the widely-recycled "Gartner: 60% of teams regret microservices adoption" statistic — round 1 search surfaced only aggregator blogs repeating it, never a traceable Gartner report.
- Look for a same-stack (DDD + Clean Architecture .NET + modular React) 6-12-month team retrospective specifically, rather than generic Clean-Architecture-only or modular-monolith-only accounts.
- Chase the Jason Taylor template GitHub discussion further for any linked real-project experience reports (vs. purely stylistic debate).

## Searched for but could not find
- A retrospective specifically combining DDD tactical patterns + Clean Architecture .NET backend + a modular/micro-frontend React frontend as one integrated stack decision (all sources found treat these as separate topics).
- Any source with concrete build-time/CI-time numbers for a .NET modular monolith at a stated module count or LOC.

## Round stop reason
coverage — round 1 secured primary/near-primary sources across most requested sub-topics (Clean Architecture template criticism from the author's own repo, DDD tactical-pattern adoption sequencing, modular-monolith boundary-enforcement tooling, and two well-known monolith-reversion case studies); proceeding to round 2 specifically to check the suspect Gartner statistic and search for a closer-matching same-stack retrospective.
