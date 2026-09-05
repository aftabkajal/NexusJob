# Dimension: landscape — Round 2

## Findings
- claim: Next.js Multi-Zones (official Next.js feature) lets teams run multiple independent Next.js applications under a single domain, each owning a set of paths, allowing independent builds/frameworks per zone at the cost of hard navigations between zones — documented directly by the Next.js team.
  source: https://nextjs.org/docs/app/guides/multi-zones
  publisher: Vercel / Next.js official docs
  pub_date: undated (living docs page)
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://vercel.com/templates/next.js/microfrontends-multi-zones

- claim: As of early 2026, there was no native micro-frontend integration for the Next.js App Router, which several practitioner sources flag as a meaningful gap given that "70% of new projects use the App Router."
  source: https://dotpingdesign.com/micro-frontends-2026-module-federation-multi-zones/
  publisher: DotpingDesign (practitioner/agency blog)
  pub_date: 2026 (undated within year)
  accessed: 2026-09-05
  confidence: low
  class: landscape
  independent_second_source: none found — the "70%" adoption figure is unsourced in the article; treat as an unverified claim, not the App Router/multi-zone gap itself which is corroborated by the official Next.js docs only describing Pages/App Router caveats generally.

- claim: .NET Aspire 13 was released at .NET Conf 2025 (November) and expanded beyond .NET to also support Python and Node.js orchestration/dev-loop tooling; multiple 2026 sources describe it as having matured past its "rough around the edges" early releases into something usable for production-oriented dev-to-cloud workflows.
  source: https://belitsoft.com/net-development-services/net-aspire
  publisher: Belitsoft (vendor/consultancy blog)
  pub_date: 2026 (undated within year)
  accessed: 2026-09-05
  confidence: low
  class: version
  independent_second_source: https://codewithmukesh.com/blog/aspire-for-dotnet-developers-deep-dive/

- claim: A named production-adoption anecdote for .NET Aspire — a Microsoft-internal team facing complex onboarding/integration defects reportedly migrated their system to Aspire "within days" and could then launch the whole system from a single IDE with live end-to-end tracing — is cited in secondary blog coverage but without a named team, product, or verifiable source document.
  source: https://belitsoft.com/net-development-services/net-aspire
  publisher: Belitsoft (vendor/consultancy blog)
  pub_date: 2026 (undated)
  accessed: 2026-09-05
  confidence: low
  class: landscape
  independent_second_source: none found — this reads as secondhand/marketing-register retelling (unnamed source, no production numbers); flagged as unverified belief per source-craft rules, not a confirmed fact.

- claim: A live GitHub issue on the microsoft/aspire repo ("Production-First focus right from the start") indicates the Aspire team and community are still actively discussing/iterating on production-readiness gaps as of the current tracked issue set, i.e., production-hardening is an ongoing, not-yet-fully-closed effort.
  source: https://github.com/microsoft/aspire/issues/9964
  publisher: microsoft/aspire GitHub repo (primary/official)
  pub_date: undated (open issue)
  accessed: 2026-09-05
  confidence: medium
  class: ecosystem-signal
  independent_second_source: none found — single primary-source issue, but it is itself a primary source (official repo), which partially offsets the two-source requirement for this "still maturing" characterization.

## Leads for next round
- None pursued further — round cap for this dimension reached.

## Searched for but could not find
- A verifiable, named production case study for either .NET Aspire or Feature-Sliced Design at meaningful scale (multiple sources make the claim in the abstract; none supply company name + numbers + timeline).
- A primary source (official survey, e.g. State of JS/State of Frontend) confirming the "70% App Router adoption" or "63% of 50+-dev orgs run monorepos" statistics cited by secondary blogs.

## Round stop reason
round cap reached — the 2-round budget for this dimension is exhausted; remaining open items (named production case studies) are flagged as gaps rather than pursued further.
