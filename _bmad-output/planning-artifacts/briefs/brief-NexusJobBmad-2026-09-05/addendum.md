---
title: Addendum: NexusJob
status: draft
created: 2026-09-05
updated: 2026-09-05
revisions:
  - 2026-09-05 — Architecture phase closed the open items; BFF (Duende+YARP) and Nx/Turborepo notes superseded by the spine (AD-12, AD-16). See the architecture spine for the binding decisions.
---

# Addendum: NexusJob

Supporting depth that belongs to the downstream architecture document rather than the brief itself. Carried forward from `technical-ddd-net-react-clean-architecture-modular-2026-09-05/research.md`.

## Accepted Architecture (source of truth: linked research)

**Backend** — modular monolith:
- One module per DDD bounded context (candidate contexts for this domain: Job Postings, Applications, Identity/Company).
- Compiler-enforced "Contracts-only" public surface between modules — no cross-module access except through a module's published Contracts.
- Inside each module: default to **Vertical Slice Architecture**. Reserve full 4-layer Clean Architecture ("Pragmatic Clean Architecture") only for modules with genuinely complex, long-lived domain logic.
- Boundary enforcement via NetArchTest or ArchUnitNET in CI — not developer discipline alone.

**Frontend** — Feature-Sliced Design (FSD):
- Layered app/pages/widgets/features/entities/shared, slices organized by business capability, intended to mirror the backend's bounded contexts.
- The research flags this frontend-mirrors-backend mapping explicitly as an **unvalidated hypothesis** with zero production precedent cited — treat it as something to test on this project, not an assumed-correct pattern. Worth a deliberate checkpoint during architecture/implementation to confirm it's actually paying off rather than just adding indirection.

## Library/Tooling Implications Flagged by Research

- **Avoid MediatR and AutoMapper as defaults** — both went commercial in 2025 (live licensing/cost decision, not a free default). Alternatives: an OSS mediator (e.g., Mediator, SwitchMediator) or an in-house dispatcher; Mapster or Mapperly instead of AutoMapper. *(Architecture decision, AD-14: no mediator library at all — Minimal API endpoints call slice handlers directly; hand-written mapping. Revisit only under pipeline-behaviour pressure.)*
- **BFF pattern** (Duende BFF + YARP) for auth — tokens held in encrypted HttpOnly cookies rather than browser JS. *(**Superseded by architecture spine AD-12.** The security outcome is kept — session in an `HttpOnly; Secure` cookie, never in JS — but via **same-origin hosting** (the ASP.NET Core host serves the SPA and the API from one origin) rather than a separate Duende BFF + YARP proxy. The research explicitly permits "a custom minimal-API BFF"; the same-origin host is its lightest form, and it removes a commercial dependency and all cross-origin/CORS handling.)*
- **NSwag or Kiota** for OpenAPI-driven, CI-regenerated TypeScript clients rather than hand-maintained DTOs. *(Architecture decision, AD-15: **NSwag**, with the host pinned to emit OpenAPI 3.0 — .NET 10's default 3.1 output is not reliably consumed by NSwag's TS generator. Orval is the logged fallback.)*
- Frontend boundary tooling: Nx, or Turborepo if the scope stays small (likely appropriate here given v1's minimal scope). *(**Superseded by architecture spine AD-16.** v1 has a single frontend package, so no monorepo tool is needed — the FSD layer boundaries are enforced by `eslint-plugin-boundaries` in CI instead.)*

## Rationale (why this over alternatives)

- Modular monolith captures most of DDD's structural benefit without microservices' operational cost, and is an easier stepping-stone to microservices later than retrofitting boundaries after the fact.
- Splitting by bounded context (not by data entity/table) avoids the "distributed monolith" failure mode seen in real-world reversions (cited: Segment, Prime Video).
- Vertical Slice + Clean Architecture combination is a named consensus position among multiple .NET architecture authorities (e.g., Milan Jovanović; Ardalis's reference template now ships both variants).

## Tradeoffs / Risks Flagged by Research

- DDD costs more code up front, and cross-entity/cross-slice logic can stay awkward — noted from a first-person account cited in the research, not just theoretical.
- The FSD-mirrors-bounded-context frontend mapping has no cited production precedent.
- Applying full Clean Architecture ceremony to what is genuinely simple CRUD is explicitly warned against in the research — relevant here since NexusJob's core loop (post/search/view/apply) is mostly CRUD. The interesting architectural work is in how bounded contexts are split and enforced, not in adding layers to a job-posting form.
- Several version/ecosystem-specific claims in the source research (tooling versions, pricing) were already flagged stale at time of publish (2026-09-05) — re-verify current state of MediatR/NSwag/licensing before implementation begins.

## Open Items for the Architecture Phase — resolved 2026-09-05

All three closed by the architecture spine (`_bmad-output/planning-artifacts/architecture/architecture-NexusJobBmad-2026-09-05/ARCHITECTURE-SPINE.md`):

- ~~Confirm concrete bounded-context boundaries~~ → **AD-4**: three modules — **Identity**, **JobPostings**, **Applications**. Split by bounded context, not data entity. Contract graph fixed at `Applications → {Identity, JobPostings}` and `JobPostings → Identity` (AD-19).
- ~~FSD-mirrors-backend: adopt or pilot~~ → **AD-17**: **piloted on the Applications capability only**, end to end, with a checkpoint before propagating.
- ~~Re-verify MediatR/AutoMapper/NSwag licensing and version state~~ → done in the spine's reviewer gate (2026-09-05, web-verified): MediatR/AutoMapper still commercial → no mediator library (AD-14); NSwag chosen with the OpenAPI-3.0 pin (AD-15); stack versions bumped to current (.NET 10, PostgreSQL 18, React Router v8, etc.).
