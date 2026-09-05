# PRD Quality Review — PRD: NexusJob

## Overall verdict

This PRD is well-calibrated to its stated stakes: a deliberately tiny two-sided job board whose real product is practicing spec-driven development. It has a real thesis (§1), features that trace to that thesis with no gold-plating, and honest scope management (Non-Goals, Assumptions Index, Open Questions, a `[NOTE FOR PM]`) rather than silent omission. Done-ness clarity is generally strong — every FR has testable consequences and the PRD is free of "handles gracefully"-style hedging — but two cross-FR gaps (email-uniqueness scope across roles, and Job Posting field validation) are neither specified nor flagged as assumptions/open questions, which is the one place downstream implementation would have to guess. Nothing here reads as theater; nothing is over- or under-formalized for the shape of the project.

## Decision-readiness — strong

Decisions are stated as decisions, not softened into "considerations." §4.1 explicitly says "no SSO, no email verification, no admin approval of accounts (confirmed)"; FR-6's Out of Scope says attachments/withdrawal/status are "confirmed out of scope for v1." Trade-offs are named with what's given up, not just what's chosen: FR-4 states plainly "No filtering by location, salary, or other facets is available," and the counter-metric SM-C1 (§8) explicitly pushes back against SM-2, warning that "more architecture is not the win condition" — a genuine tension surfaced rather than smoothed over.

The three Open Questions (§9) are genuinely open, not rhetorical-with-an-answer-attached: OQ-1 (edit/delete lifecycle) is echoed by a `[NOTE FOR PM]` in §6.2 that defers rather than resolves it ("revisit if the core loop feels incomplete"); OQ-2 (minimum applicant fields, "is a name required at signup?") is a real gap the PRD itself surfaces rather than silently assuming Company sees a name that was never collected at FR-2 sign-up. No section reads as "every choice balances everything" — this PRD is comfortable naming what it is not doing.

### Findings
None — no findings that would change how this dimension reads.

## Substance over theater — strong

No persona theater: three JTBD entries (§2.1) — builder, Company, Job Seeker — each drives concrete FRs, and the terse framing ("Nothing more.") signals the personas were kept minimal on purpose, not padded for the appearance of thoroughness. No innovation/differentiation section exists, and Non-Goals (§5) explicitly disclaims market competition — appropriate rather than an omission. NFRs (§7) are specific, not boilerplate: "The build must fail if any module accesses another module outside its published contract surface — enforced by an automated architecture test (e.g. NetArchTest or equivalent)" is a testable constraint, not a "must be scalable" placeholder. The Vision (§1) could not be swapped into another PRD unchanged — it names the actual deliverable ("a practice vehicle for AI-assisted, spec-driven development") and specifically explains why the domain is sized the way it is.

### Findings
None.

## Strategic coherence — strong

The thesis is explicit in §1: the domain is "intentionally small but structurally real" so that the process of splitting bounded contexts — not the feature list — is the thing being practiced. Feature prioritization follows the thesis: all 7 FRs are exactly the core loop (post → search → view → apply → view applicants), with no adjacent scope-creep (no messaging, no matching, no admin tooling — all pushed to §5/§6.2). Success Metrics validate the thesis rather than measuring activity: SM-1 is the working core loop, SM-2 is enforced module boundaries (the actual point of the exercise), SM-3 is reflective learning capture — none of these is a vanity/DAU-style metric. A counter-metric (SM-C1) is present and directly checks SM-2 against over-engineering. MVP scope kind reads as a hybrid: capability-spec-like on the product surface (matches "hobby/solo, single builder") with just enough UJ framing to keep the two in-product roles concrete — the scope logic matches the stated purpose rather than reading as a backlog with headings.

### Findings
None.

## Done-ness clarity — adequate

Most FRs are unforgiving-review-proof: FR-1/FR-2 specify exact registration/login failure conditions and explicitly ban the "reveals which field was wrong" leak; FR-6 pins down exactly one Application per (Job Seeker, Job Posting) pair; FR-7 pins down ownership scoping ("A Company can only view Applicants for Job Postings it created"). No instances of "reasonable performance," "user-friendly," or "handles X gracefully" were found anywhere in the FR set — this is the strongest dimension mechanically. That said, two gaps sit outside any individual FR's consequences and are not flagged as `[ASSUMPTION]` or Open Questions, unlike the PRD's own established pattern of flagging exactly this kind of ambiguity elsewhere:

### Findings
- **medium** Email uniqueness scope is unspecified across roles (§4.1, FR-1/FR-2) — FR-1 says registration fails "if the email is already registered as a Company," FR-2 says the parallel for Job Seeker, but nothing states whether the same email can register as both a Company and a Job Seeker (uniqueness scoped per-role) or must be globally unique. This is a data-model decision (one identity table vs. two, or a cross-role uniqueness constraint) that architecture cannot infer safely either way. *Fix:* add one line to §4.1 or a `[NOTE FOR PM]`/`[ASSUMPTION]` stating whether email uniqueness is global or per-role.
- **medium** Job Posting field validation is unspecified (§4.2, FR-3) — FR-3 requires "at minimum a title and description" but gives no bound on emptiness, length, or format (can description be one character? Is there a max length?). Every other FR in the PRD gives a testable bound; this one is the exception. *Fix:* one Consequences bullet, e.g. "title and description must be non-empty" (length caps can be an implementation default if the PRD doesn't want to commit to one).
- **low** FR-4's match semantics hedge with "or equivalent basic match" (§4.2) — the rest of the FR set is precise about exact behavior; this phrase leaves the actual matching algorithm undefined. Low severity because for a keyword search on a two-table hobby app the practical difference is small. *Fix:* either commit to "case-insensitive substring match" plainly, or note it's an implementation-level choice.

## Scope honesty — strong

Non-Goals (§5) does real work — it rules out ATS-style hiring workflow, multi-tenant company accounts, messaging, and mobile, each a plausible scope-creep direction for a job board specifically. `[ASSUMPTION]` tags are used exactly where the user wasn't directly asked (FR-4/FR-5 open-browsing assumption; FR-6 duplicate-prevention assumption) and both round-trip into the Assumptions Index (§10) — see Mechanical notes. The one `[NOTE FOR PM]` (§6.2) sits at a real deferred tension (whether posting lifecycle management is needed) rather than a safe checkpoint. Open-items density (3 Open Questions + 2 assumptions + 1 NOTE FOR PM = 6 total) is proportionate to a hobby/solo, non-launch PRD — this would be a blocker-level density on a green-light-to-build enterprise PRD, but is appropriate here.

### Findings
None.

## Downstream usability — adequate

§0 states this PRD feeds "downstream architecture and implementation workflows," so this dimension is chain-top, not standalone, and matters more than it would for a one-off document. The Glossary (§3) is used consistently — "Job Posting," "Job Seeker," "Company," "Application," "Applicant" all appear with stable capitalization and meaning across every FR that uses them, with no synonym drift observed. FR IDs (FR-1…FR-7), UJ IDs (UJ-1, UJ-2), and SM IDs (SM-1…SM-3, SM-C1) are each contiguous and unique, and cross-references resolve (SM-1 → "Validates FR-1 through FR-7"; the Product Brief link in §0 resolves to an actual file on disk). Each Feature subsection (§4.1–4.3) is self-contained enough to be pulled out alone.

### Findings
- **low** §2 skips from "2.1 Jobs To Be Done" straight to "2.3 Key User Journeys" with no 2.2 — a numbering gap that could confuse a downstream skill doing section-anchored extraction. *Fix:* renumber 2.3 to 2.2, or confirm a 2.2 section was intentionally cut and remove the gap.

## Shape fit — strong

This is a hobby/solo, chain-top PRD with a real (if minimal) two-sided consumer shape. The rubric's guidance to keep UJs load-bearing for consumer/multi-stakeholder products is satisfied without over-formalizing: exactly two UJs, each with a named protagonist carrying context inline (Raj, a hiring manager; Amara, job-hunting), and no UJ density inflation beyond what the two in-product roles need. The primary persona (the builder) correctly has no UJ of its own — its JTBD is about the practice exercise, not an in-product journey, and the PRD doesn't force one. Enterprise-cluster sections (compliance, monetization, stakeholder maps, competitive differentiation) are correctly absent rather than stubbed in for template completeness, consistent with the explicit Non-Goals framing. No over-formalization (excess personas, elaborate NFR ceremony) and no under-formalization (a consumer-shaped product with zero UJs) was found.

### Findings
None.

## Mechanical notes

- **Glossary drift:** none found. All five Glossary terms (Company, Job Seeker, Job Posting, Application, Applicant) are used with consistent capitalization and meaning throughout §4–§10.
- **ID continuity:** FR-1…FR-7 contiguous and unique; UJ-1/UJ-2 contiguous; SM-1…SM-3 plus SM-C1 contiguous. No gaps or duplicates found. Cross-references (SM validating specific FRs, §0 linking to the Product Brief) resolve correctly — the referenced `brief.md` exists on disk at `_bmad-output/planning-artifacts/briefs/brief-NexusJobBmad-2026-09-05/brief.md`.
- **Assumptions Index roundtrip:** clean. Both inline `[ASSUMPTION]` tags (§4.2 covering FR-4/FR-5; §4.3 FR-6) are indexed in §10, and both §10 entries correspond to an inline tag — no orphans either direction.
- **UJ protagonist naming:** both UJs carry a named protagonist with inline context (Raj / hiring manager / small startup; Amara / job-hunting) — no floating UJs.
- **Section numbering:** §2 jumps from 2.1 to 2.3 with no 2.2 present (see Downstream usability finding above). No other numbering gaps found in §§1–10.
- **Required sections for stakes/type:** present and appropriately scoped for a hobby-stakes, chain-top capability-spec-shaped PRD — Vision, Target User/JTBD/UJs, Glossary, Features/FRs, Non-Goals, MVP Scope, Cross-Cutting NFRs, Success Metrics, Open Questions, Assumptions Index. No enterprise-cluster sections present, correctly.
