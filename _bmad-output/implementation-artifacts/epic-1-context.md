# Epic 1 Context: Accounts and the running skeleton

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

This epic stands up the entire delivery substrate and proves the project's central architectural claim before feature work depends on it. A Company and a Job Seeker can each register and sign in; the app is deployed, reachable, and served same-origin (SPA plus API from one origin); and cross-module boundaries are enforced by CI from the first commit. All the scaffolding delivered here — the modular-monolith solution, the Identity module, cookie auth, the architecture-test build gate, the OpenAPI-to-TypeScript client pipeline, the health endpoint, local Docker Compose, the GitHub Actions pipeline, the from-scratch design-token system, and the role-aware shell with the Sign up / Log in surface — is justified by shipping the two account-creation requirements, not treated as a standalone setup phase. Landing the enforced boundary now (a primary success criterion) means every later story sits on a structure that is continuously verified rather than maintained by discipline.

## Stories

- Story 1.1: Walking skeleton with a CI-enforced module boundary gate
- Story 1.2: From-scratch design system and the role-aware application shell
- Story 1.3: Company registration and sign-in
- Story 1.4: Job Seeker registration and sign-in

## Requirements & Constraints

- A Company registers with an email/password unique within Company accounts only; a Job Seeker does the same in an account space fully independent of Company accounts. The same email may hold one Company and one Job Seeker account as unrelated identities.
- Duplicate-email registration for a role is rejected with no second account created.
- Sign-in succeeds only on a matching email/password pair; failure returns one generic message that never reveals which field was wrong.
- No email verification or approval gates activation — an account is usable immediately.
- Passwords are hashed at rest, never logged or stored in plaintext.
- Single desktop-first web app, light mode only, no responsive breakpoints, no native app.
- The build must fail on any cross-module boundary violation via an automated CI architecture test — this gate is a primary deliverable, not optional tooling.
- The app must be deployable and usable end-to-end (health-checkable, same-origin, container-imaged) as the foundation for the core loop finished in later epics.
- Microcopy is formal and professional: complete sentences, terminal punctuation, no exclamation marks or emoji. Prescribed strings include "This email is already registered as a {Company/Job Seeker}." and "That email and password don't match. Please try again."
- Accessibility floor is WCAG 2.1 AA: visible focus indicator on every interactive element, full keyboard operability, focus order matching visual reading order, reduced-motion support, and field errors programmatically associated with their field (not conveyed by color alone).

## Technical Decisions

- Backend is a modular monolith: `NexusJob.Host` plus `NexusJob.Modules.{Identity,JobPostings,Applications}`, a `.Contracts` project per module, and `NexusJob.ArchitectureTests`. The Host is the only project referencing module implementations; a module implementation references only other modules' `.Contracts`; a `.Contracts` project references no implementation. Each module exposes `AddXxxModule` / `MapXxxModule` and the Host wires them and does nothing else module-specific.
- Inside a module, organize by feature slice (endpoint, handler, request/response, validation together). No Domain/Application/Infrastructure layering, no mediator library — endpoints call slice handlers directly; cross-cutting concerns are middleware or endpoint filters. Do not add ceremony beyond what CRUD needs.
- Architecture tests (ArchUnitNET, build-breaking) fail on: a module implementation depending on another module's non-Contracts assembly; a `.Contracts` assembly depending on any implementation; any non-Host project depending on a module implementation; a `FromSqlRaw`/`ExecuteSql*` call naming a schema outside the caller's own. Frontend equivalent is `eslint-plugin-boundaries` encoding the FSD layer order, also build-breaking.
- Persistence: one PostgreSQL 18 database, schema-per-module (`identity` for this epic). One `DbContext` per module mapping only its own entities with `search_path` set to its schema; per-module EF migration history; no foreign key crosses a schema. Primary keys are `Guid.CreateVersion7()` generated in app code (string only at the JSON edge). Timestamps are UTC ISO-8601, stored `timestamptz`, named `*_at` / `*At`. DB identifiers are `snake_case`.
- Identity owns two independent tables in the `identity` schema: `company_account` (id, email, password_hash, display_name) and `job_seeker_account` (id, email, password_hash, full_name), each with a unique constraint on email within that table. All `/api/auth/*` routes belong to Identity.
- Auth: ASP.NET Core cookie authentication without the full Identity framework. One `HttpOnly; Secure; SameSite=Lax` cookie carrying `NameIdentifier` = account id and an `account_type` claim (`company` | `job_seeker`); caller identity is read only from `NameIdentifier`. Passwords hashed with `Microsoft.AspNetCore.Identity.PasswordHasher` (PBKDF2-HMAC-SHA256), `IterationCount` pinned explicitly to at least 600,000. State-changing requests require an antiforgery token (double-submit, `X-CSRF-TOKEN` header, seeded by `GET /api/auth/csrf`). Credentials, hashes, and cookie values are never logged.
- Data Protection keys persist to a `data_protection_keys` table in the `public` schema, owned by the Host, so cookies survive restarts.
- One write request mutates exactly one module's schema in a single `SaveChanges` (register touches Identity only).
- HTTP contract: RFC 9457 ProblemDetails on every non-2xx; success responses return the resource representation directly, no envelope. Paginated lists use `Page<T> { items, page, pageSize, total }`, offset pagination, `page` 1-based, `pageSize` default 20 / max 100. REST paths are lowercase plural nouns; the `/api` prefix follows the owning module.
- Input validation is built-in .NET 10 minimal-API validation (`AddValidation()`, DataAnnotations). FluentValidation deferred.
- The Host serves the built React bundle as static files and `/api` routes from one origin — no BFF proxy, no YARP, no CORS handling. The Host emits one OpenAPI 3.0 document (generator pinned to 3.0 output); the frontend TypeScript client is generated from it by NSwag in CI and is the only permitted client-side description of the API. Hand-written types duplicating the API are forbidden.
- Frontend is Vite 8 + React 19 + TypeScript with Feature-Sliced Design layers `app → pages → widgets → features → entities → shared`, imports only downward. Server state via TanStack Query with query keys owned by the `entities/<noun>` slice. Routing via React Router v8. No global client-state store. The generated client (`shared/api`) is imported only from an `entities/*/api` or `features/*/api` segment.
- Operability floor: `GET /health` returns process liveness plus a `SELECT 1` DB probe. Structured JSON logging to stdout with credential/hash/cookie redaction. All config and secrets (DB connection string, Data Protection key-ring config, hash iteration count) come from environment / .NET configuration — nothing secret in the image or repo.
- CI (GitHub Actions): build backend, run architecture tests, run unit and integration tests (integration against real Postgres via Testcontainers), run the frontend boundary lint, regenerate the OpenAPI TypeScript client, and build the container image; any failure fails the pipeline.
- Deployment: a single container image (Host serving API + SPA build) plus one PostgreSQL instance. Docker Compose for local (`app` + `postgres`). Per-module migrations applied as a release step before the new image serves traffic. One hosted environment for v1; hosting platform undecided.
- Auth endpoints in this epic beyond register/login: `GET /api/auth/me` (returns caller account type and display name, resolved from `NameIdentifier` only), `POST /api/auth/logout` (clears the cookie), `GET /api/auth/csrf`.

## UX & Interaction Patterns

- One shared shell and nav bar for both roles; nav items and primary actions are role-aware based on the signed-in account type, while browse/search stay open to signed-out visitors. Never a dead or disabled nav item pointing at a surface the current viewer cannot use. After registration or sign-in the shell re-renders into the correct role's signed-in state.
- This epic builds the shell, the Home / Search landing route (search behavior arrives in Epic 2 — an empty state is acceptable here), and the Sign up / Log in surface with the role toggle.
- `auth-role-toggle`: a two-option pill ("Company" / "Job Seeker"), exactly one active, active option using the primary fill and the `full` (pill) radius — the only place that radius is used. Operable via Tab + arrow keys with Enter/Space to select. Switching roles clears role-specific field errors but keeps entered email, password, and name. Sign-up mode shows a required name field above email, labeled "Company name" or "Full name" per the active role; log-in mode never shows the name field. This name is the source of the Company name on postings and the Applicant name in the applicants list.
- Form validation timing: on blur (per field) and again on submit, never per keystroke. Errors render inline below the field in the danger color, associated with the field for assistive tech. Duplicate-email registration shows the role-specific message inline under the email field with other entered values retained; failed sign-in shows the generic mismatch message.
- Design tokens are built from scratch and live in the `shared/` layer; no component defines a color, font size, radius, or spacing value outside them. Implement the Nexus Indigo palette (background, surface, primary, accent, text-primary, text-secondary, border, success + subtle, danger + subtle), the Inter-only type ramp, the rounding scale (6 / 10 / 16 / 24 / full), and the 4px-base spacing scale plus named gaps (gutter 32, editorial-gap 96) exactly as given in Story 1.2 / DESIGN.md.
- Layout: content sits in a fixed 1120px desktop-first max-width container, no breakpoints, light mode only.
- The accent color is used only for the single primary action per surface (here, the auth submit) — never for chrome, nav highlights, or decoration. Success/danger colors are always paired with a text label at AA contrast, never a color-only signal.
- Motion is subtle and purposeful only (fade/slide on state change); `prefers-reduced-motion` suppresses all such transitions.

## Cross-Story Dependencies

- Story 1.1 (scaffold, boundary gate, CI, health, Docker Compose, same-origin hosting) is the foundation for every other story here and in later epics.
- Story 1.2 (design tokens and the role-aware shell) must land before the auth surface in 1.3 and 1.4, which render inside the shell and reuse the `auth-role-toggle` and validation patterns.
- Story 1.3 establishes the `identity` schema, `DbContext`, migration history, cookie/CSRF plumbing, `PasswordHasher` config, the Data Protection key table, the `/api/auth/*` routes, and the OpenAPI-to-NSwag client pipeline; Story 1.4 extends the same module and endpoints with the second account table and reuses all of it.
- The `IIdentityApi` cross-module Contract is defined and consumed only in Epics 2 and 3; this epic needs only the Identity implementation and its persistence.
- Epic 2 builds on this epic; Epic 3 builds on Epics 1 and 2. No story here depends on a later epic.
