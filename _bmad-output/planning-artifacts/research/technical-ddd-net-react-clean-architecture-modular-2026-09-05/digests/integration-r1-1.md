# Dimension: integration — Round 1

## Findings
- claim: The Backend-for-Frontend (BFF) pattern is becoming the standard approach for securing modern SPAs against a .NET backend: the BFF holds tokens server-side in an encrypted HttpOnly cookie rather than exposing them to browser JS, avoiding XSS-based token theft.
  source: https://docs.duendesoftware.com/bff/
  publisher: Duende Software (maintainers of Duende IdentityServer/BFF)
  pub_date: undated
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://duendesoftware.com/blog/20210326-bff

- claim: Storing OAuth access tokens in browser storage (localStorage/sessionStorage/JS memory) exposes them to theft via XSS and compromised npm supply-chain packages; the BFF pattern avoids this by running the auth-code flow server-side and reverse-proxying API calls with the token attached.
  source: https://duendesoftware.com/blog/20260414-the-cookie-apocalypse-already-happened
  publisher: Duende Software
  pub_date: 2026-04
  accessed: 2026-09-05
  confidence: medium
  class: ecosystem-signal
  independent_second_source: https://nestenius.se/net/bff-in-asp-net-core-2-the-bff-pattern-explained/

- claim: Microsoft's YARP (Yet Another Reverse Proxy), built on ASP.NET Core/Kestrel, is commonly used as the implementation vehicle for the BFF pattern in .NET, forwarding SPA API calls to downstream services/bounded contexts.
  source: https://medium.com/@amhemanth/implementing-the-backends-for-frontends-bff-pattern-with-microsofts-yarp-and-net-minimal-apis-41c391974f43
  publisher: Medium (independent author)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://nestenius.se/net/bff-in-asp-net-core-4-implementing-a-bff-from-scratch/

- claim: NSwag is the long-established Swagger/OpenAPI toolchain for .NET that generates OpenAPI specs from ASP.NET Core controllers AND generates C#/TypeScript clients from OpenAPI documents; Kiota is Microsoft's newer, multi-language-focused alternative positioned as more tightly integrated with the Microsoft ecosystem.
  source: https://github.com/RicoSuter/NSwag
  publisher: GitHub (RicoSuter/NSwag, primary maintainer repo)
  pub_date: undated
  accessed: 2026-09-05
  confidence: high
  class: pattern
  independent_second_source: https://codingdroplets.com/kiota-vs-nswag-vs-refitter-dotnet-typed-api-client-2026

- claim: In practice, API contracts break not because tests fail but because contracts silently drift — a field added without warning, a type changed (bool→string), a new default introduced, or a producer deployed before consumers are ready; frontends often assume undocumented backend behavior, and a renamed field or unexpected status code can crash the client.
  source: https://www.aakashx.com/blog/api-contracts-that-survive-production/
  publisher: independent engineering blog
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://scalewithchintan.com/blog/versioning-microservices-without-breaking-clients

- claim: Consumer-driven contract testing (e.g., Pact) is the recommended mitigation for API/contract drift between frontend consumers and backend producers, verifying producer/consumer agreement in CI before deploy.
  source: https://mokapi.io/resources/blogs/guard-your-api-contracts
  publisher: independent (Mokapi, a contract/mock-testing tool vendor)
  pub_date: undated
  accessed: 2026-09-05
  confidence: low
  class: pattern
  independent_second_source: none found

- claim: For React SPA + .NET backend integrations, over-fetching (backend returns more than the client needs) and under-fetching (client must chain multiple calls to assemble a view) are commonly reported problems when APIs are modeled around backend/domain data shapes rather than frontend view needs; teams sometimes add a GraphQL aggregation layer in front of REST endpoints to mitigate this.
  source: https://medium.com/@decodinggtech/rest-api-over-fetching-the-silent-performance-killer-every-developer-ignores-d5f8b0a1930c
  publisher: Medium (independent author)
  pub_date: undated
  accessed: 2026-09-05
  confidence: low
  class: pattern
  independent_second_source: https://blog.stackademic.com/what-is-over-fetching-and-under-fetching-in-apis-96628332c64c

- claim: Shared-type duplication between frontend and backend ("type drift") is reported as a leading cause of silent production bugs; the API-first mitigation is to regenerate frontend TypeScript types from the backend's OpenAPI schema in CI on every change so both sides stay aligned automatically (rather than hand-maintained duplicate DTOs).
  source: https://dev.to/dmitrii-verbetchii/api-first-in-practice-how-we-made-frontend-types-predictable-and-stable-332c
  publisher: DEV Community (practitioner account)
  pub_date: undated
  accessed: 2026-09-05
  confidence: medium
  class: pattern
  independent_second_source: https://bit.dev/blog/sharing-types-between-your-frontend-and-backend-applications-l5qih48g/

## Leads for next round
- Follow up on real GitHub issue friction with NSwag/Kiota generated clients (breaking changes across major versions, serialization bugs) — found in generic search, need issue-level detail.
- Chase whether DDD bounded-context boundaries are ever reported as mapping cleanly to React micro-frontend/module boundaries in a named production case study (round 1 only surfaced generic conceptual pieces, not a concrete account).
- Module Federation 2.0 (2026) claims about TypeScript type-sharing across micro-frontend remotes — verify against a second, independent source.
- Duende BFF "cookie apocalypse" post (2026-04) — worth a closer read for what specifically broke/changed recently in cookie-based SPA auth.

## Searched for but could not find
- A named company/production engineering blog explicitly describing how their DDD bounded-context boundaries in a .NET backend were mapped (or deliberately NOT mapped) to their React frontend's module boundaries.
- Independent, dated benchmark/performance numbers for gRPC-web adoption in this specific stack (.NET + React) — not surfaced in round 1 at all.

## Round stop reason
coverage — round 1 produced usable primary/near-primary sources (Duende, NSwag/GitHub, YARP) across most of the requested sub-topics (BFF/auth, codegen tooling, contract drift, over/under-fetching, type duplication); proceeding to round 2 to chase the specific leads above (GitHub issue-level friction, bounded-context-to-frontend-module mapping, micro-frontend type sharing) rather than re-querying broadly.
