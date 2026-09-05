# Dimension: integration — Round 2

## Findings
- claim: NSwag v14 introduced multiple breaking changes for consumers of generated clients: NJsonSchema generator settings moved under a new "SchemaSettings" property, nswag.json now only supports .csproj-based spec generation, and WebApiToOpenApiCommand was removed — teams upgrading across major NSwag versions hit real breakage in their codegen pipeline, not just the generated API surface.
  source: https://github.com/RicoSuter/NSwag/issues/4524
  publisher: GitHub (RicoSuter/NSwag issue tracker, primary)
  pub_date: undated (v14 release cycle)
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://github.com/RicoSuter/NSwag/releases

- claim: Generated NSwag C# clients have shipped with concrete correctness bugs in production-relevant paths: route/path template parameters not substituted correctly (e.g. OData-style `{key}` segments), BaseUrl not set properly, and a new JsonSerializerOptions instance being constructed on every client instantiation (a problem given typed HttpClients are transient-lifetime in .NET DI, causing repeated allocation/perf issues).
  source: https://github.com/RicoSuter/NSwag/issues/4587
  publisher: GitHub (RicoSuter/NSwag issue tracker, primary)
  pub_date: undated
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://github.com/RicoSuter/NSwag/issues/4637

- claim: Module Federation 2.0 is described (by a 2026 practitioner source) as addressing the "biggest operational headaches" of v1 micro-frontends — specifically citing improved singleton config, TypeScript type sharing across remotes, error boundaries, and a manifest protocol — but this is a single blog's characterization, not independently corroborated with production metrics.
  source: https://blog.codercops.com/blog/micro-frontends-module-federation-architecture-2026
  publisher: CODERCOPS (independent blog)
  pub_date: 2026 (undated within year)
  confidence: low
  class: ecosystem-signal
  independent_second_source: none found — a second 2026 source (techoral.com) describes MF2/Rspack generally but does not corroborate the specific "type sharing solves the biggest headache" claim

- claim: Practitioners advise that before adopting micro-frontends (React + Module Federation) at all, teams should first agree on a one-page integration contract covering shared React major version, shared design tokens, error-boundary ownership, and rollback ownership when a remote fails — and that inability to agree on these means the team isn't ready for micro-frontends and should use a monorepo instead.
  source: https://blog.codercops.com/blog/micro-frontends-module-federation-architecture-2026
  publisher: CODERCOPS (independent blog)
  pub_date: 2026
  accessed: 2026-09-05
  confidence: low
  class: pattern
  independent_second_source: none found

- claim: No concrete, named production case study was found describing DDD bounded-context boundaries in a .NET backend being explicitly mapped (or explicitly kept independent) from React frontend module/micro-frontend boundaries. All retrieved material treats "bounded context → frontend module" mapping as a conceptual/architectural suggestion, not a reported-on production decision with outcomes.
  source: n/a (absence finding)
  publisher: n/a
  pub_date: n/a
  accessed: 2026-09-05
  confidence: n/a
  class: other
  independent_second_source: none found

## Leads for next round
- (Would pursue if a 3rd round were in budget) Search NDC/DDD Europe conference talk transcripts specifically for "bounded context" + "frontend team boundary" case studies.
- Search for Backstage/OpenAPI-diff or similar automated contract-diffing tools adopted specifically in .NET+React shops, to see if tooling has matured beyond Pact for this exact stack.

## Searched for but could not find
- Independent second source corroborating Module Federation 2.0's TypeScript type-sharing claims with production experience (only one blog surfaced this specific framing).
- Any primary engineering-blog account (named company) of bounded-context-to-frontend-module mapping succeeding or failing in production.
- gRPC-web usage/performance data specific to .NET + React (not found in either round).

## Round stop reason
novelty exhaustion — round 2 queries aimed at the round-1 leads (NSwag issue-level friction, micro-frontend type sharing, bounded-context/frontend mapping) returned either confirmatory GitHub-issue-level detail (high confidence) or repeated the same single low-corroboration blog: further querying within the 2-round budget was unlikely to surface new independent primary sources on the mapping question, so this is reported as a coverage gap rather than pursued further.
