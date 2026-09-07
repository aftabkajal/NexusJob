# Epic 2 Context: Companies post jobs; anyone can find and read them

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

This epic delivers the JobPostings module end to end and the first cross-module Contract read. An authenticated Company creates a Job Posting (title + description); it is immediately searchable by case-insensitive keyword and readable in full — including the posting Company's display name — by anyone, signed in or not. It also builds the public browse/search/detail surfaces: the Home / Search landing, the paginated Search results list, and the Job posting detail view. Identity gains its first published Contract (`IIdentityApi`) so JobPostings can resolve a company name without touching the `identity` schema, and JobPostings publishes `IJobPostingsApi` for Epic 3 consumers.

## Stories

- Story 2.1: Create a job posting
- Story 2.2: View a job posting's detail
- Story 2.3: Search job postings by keyword

## Requirements & Constraints

- A Company (authenticated session only) creates a posting with a non-empty title and a non-empty description; the owning company id comes from the caller's `NameIdentifier` claim, and `created_at` is the current UTC instant. An empty/whitespace title or description is rejected with ProblemDetails and no row written.
- Creating a posting requires an authenticated Company session and a valid antiforgery token. An anonymous caller, or a Job Seeker session, is rejected as unauthorised — no posting created.
- Search (keyword substring match over title/description) and posting detail are open to everyone — no session required. Only create-posting requires auth in this epic.
- A published posting is live and searchable immediately — no review, approval, or delay. v1 has no edit or deactivate lifecycle, but a detail request for a missing/mistyped id must still return `404`.
- Keyword search is a case-insensitive substring match run in the database (EF `Contains` / `ILIKE`) inside JobPostings — no search engine, no external index.
- Every paginated list uses the uniform shape `Page<T> { items, page, pageSize, total }`: offset pagination, `page` 1-based, request params `?page=&pageSize=`, `pageSize` default 20 / max 100. No cursor pagination.
- Microcopy stays formal (complete sentences, terminal punctuation, no exclamation/emoji): "Your job posting has been published.", "We couldn't publish this posting. Please try again.", "This posting is no longer available." Form validation is on blur and on submit (never per keystroke), inline below the field in the danger token, `aria-describedby`-associated. WCAG 2.1 AA floor unchanged (visible focus, full keyboard operability, reduced-motion).

## Technical Decisions

- **New module: `NexusJob.Modules.JobPostings`** (+ its `.Contracts` project), wired by the Host only, via `AddJobPostingsModule(IServiceCollection, IConfiguration)` / `MapJobPostingsModule(IEndpointRouteBuilder)`. Organize by vertical feature slice (endpoint delegate → handler class → request/response/validation together); no Domain/Application/Infrastructure layering, no mediator — endpoint delegates call slice handlers resolved from DI; cross-cutting concerns are middleware or endpoint filters.
- **Persistence:** `JobPosting` (`id` Guid v7 PK, `owner_company_id` Guid — a plain column, **no FK to any schema**, title, description, `created_at` `timestamptz`) in a new `job_postings` schema. Own `DbContext` mapping only JobPostings entities with `search_path` = `job_postings`, own EF migration history (`job_postings.__EFMigrationsHistory`). DB identifiers snake_case; ids are Guid in C#/Contract signatures, string only at the JSON edge. One write request mutates exactly one module's schema in a single `SaveChanges`.
- **Cross-module reads go through a Contract, never SQL.** `Identity.Contracts` publishes `IIdentityApi` — `GetCompany(Guid) -> CompanySummaryDto { Guid Id; string DisplayName }` and the batch `GetCompanies(IReadOnlyCollection<Guid>) -> IReadOnlyDictionary<Guid, CompanySummaryDto>` — a synchronous in-process service registered in DI, exposing named DTOs only (no entity types). List/table projections **must** use the batch getter; a per-row Contract call is a defect. `JobPostings.Contracts` publishes `IJobPostingsApi` — `GetPostingOwner(Guid postingId) -> Guid?` (null = no such posting) and `GetPostingSummaries(IReadOnlyCollection<Guid>) -> IReadOnlyDictionary<Guid, JobPostingSummaryDto>` — for Epic-3 consumers; no module consumes it yet. A module implementation may reference other modules' `.Contracts` and nothing else of theirs; `.Contracts` projects reference no implementation. The ArchUnitNET boundary gates enforce this and fail the build on a violation.
- **HTTP contract:** RFC 9457 ProblemDetails on every non-2xx; success responses return the resource representation directly, no envelope. REST paths are lowercase plural nouns with the `/api` prefix of the owning module — JobPostings owns `/api/job-postings/*`. The single OpenAPI 3.0 document regenerates the frontend TypeScript client (`shared/api`) via NSwag; hand-written request/response types duplicating the API are forbidden, and the CI drift gate must stay green.
- **Auth:** create-posting reads the caller from the cookie's `NameIdentifier` claim only (never an email claim, never a Contract lookup by email); a Job Seeker session calling a Company-only endpoint is `401`/`403` per the existing cookie-auth setup. Antiforgery is the existing double-submit `X-CSRF-TOKEN` scheme.
- **Frontend:** Vite + React + TypeScript, Feature-Sliced Design layers `app → pages → widgets → features → entities → shared`, imports only downward. Server state via TanStack Query with query keys owned by the `entities/<noun>` slice; the generated `shared/api` client is imported only from an `entities/*/api` or `features/*/api` segment (eslint-enforced). Routing via React Router. Design tokens are the only source of colour/type/radius/spacing.

## UX & Interaction Patterns

- **Post-a-Job surface** (authenticated Company only): reached from a "Post a Job" nav item that appears in the shell for a signed-in Company. Title field, description field, and a single primary "Publish" action rendered as the `apply-button` component in the accent colour (the one primary action per surface). On valid submit: confirmation "Your job posting has been published." (Story 2.2 adds the redirect to the new posting's detail view). On network/server failure: the entered title and description are retained, an inline banner "We couldn't publish this posting. Please try again." is shown, and a retry re-submits the same values.
- **Home / Search landing** and **Search results**: a keyword search bar; results are a paginated list of `job-card` components (title/description/company). Cold load shows skeleton rows matching the eventual layout; an empty catalog / no-match shows its own copy.
- **Job posting detail**: full title, description, and the posting Company's display name. Cold load shows a skeleton for the title/description/company block. A `404` resolves to "This posting is no longer available." with a link back to Search. (The Apply action and apply-gate on this surface are Epic 3.)
- The shell/nav is role-aware: a signed-in Company sees "Post a Job"; browse and search stay open to signed-out visitors; never a dead or disabled nav item pointing at a surface the current viewer cannot use.

## Cross-Story Dependencies

- Story 2.1 stands up the JobPostings module, its `job_postings` schema, `DbContext`, migration history, `POST /api/job-postings`, and the Post-a-Job surface. Stories 2.2 and 2.3 extend the same module with the detail (`GET /api/job-postings/{id}`) and search (`GET /api/job-postings?...`) endpoints and their public surfaces.
- Story 2.2 publishes `IIdentityApi` (Identity's first Contract) and consumes it from JobPostings' detail handler to resolve the company display name; it also publishes `IJobPostingsApi`. Story 2.1 does not need either Contract — it only writes its own schema.
- Builds on Epic 1: the cookie session and `NameIdentifier` claim (Identity), the role-aware shell and design tokens, the OpenAPI→NSwag client pipeline, the module-boundary CI gates, and the `Page<T>` / ProblemDetails HTTP conventions. Epic 3 (apply, view applicants) builds on this epic and consumes `IJobPostingsApi`.
