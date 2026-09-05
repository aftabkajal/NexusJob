---
title: Reconciliation: brief.md vs prd.md
created: 2026-09-05
---

# Reconciliation: brief-NexusJobBmad-2026-09-05/brief.md → prd-NexusJobBmad-2026-09-05/prd.md

Input: `brief.md` (with `addendum.md` reviewed as supporting/architecture-level context, correctly excluded from the PRD)

## Gaps

- **"What Makes This Different" meta-narrative dropped.** The brief's explicit self-aware framing — "Nothing, deliberately — and that is the honest answer rather than a fabricated one" — is a deliberate statement about intellectual honesty in the brief's own construction, not just a scope fact. The PRD's Non-Goals (§5, "not competing for job-board market share") preserves the *fact* but loses the *voice*: the point that admitting "no differentiation" is itself the honest/correct move for this kind of project. *Fix: add a short line to §1 Vision or §5 acknowledging that NexusJob deliberately does not attempt product differentiation, and that this is a considered choice rather than an omission.*

- **Success criterion about the spec-driven workflow's artifact trail is missing from §8 Success Metrics.** The brief lists four success criteria; the PRD's SM-1/SM-2/SM-3 map to three of them (core loop, enforced boundaries, cost/benefit articulation), but the brief's third bullet — "the spec-driven workflow itself... produces artifacts the builder can point to and explain, including the reasoning behind decisions" — has no corresponding SM entry. *Fix: add an SM-4 (or fold into SM-3) measuring that brief→architecture→implementation artifacts exist and their reasoning is traceable/explainable.*

- **"Not a researched/validated persona" caveat dropped, and PRD adds specificity the brief warned against over-reading.** The brief explicitly states neither Company nor Job Seeker is "a researched, validated user segment... minimal personas sized to what the core loop requires." The PRD's §2.1 drops this caveat entirely, and §2.3 introduces named illustrative personas (Raj, Amara) with narrative color, which — without the disclaimer — reads as more user-research rigor than the brief intended to claim. *Fix: carry the caveat into §2.1 (e.g., "these are illustrative, not researched/validated segments") so UJ-1/UJ-2 aren't misread as validated personas.*

- **Brief's flagged uncertainty about "genuine" CI-enforced module boundaries in v1 is silently resolved, not carried forward.** The brief marks this with an explicit `[ASSUMPTION]` — "confirm if this should be lighter for a first pass" — i.e., it was an open question, not a settled decision. The PRD (§6.1, §7) adopts full CI-enforced boundary enforcement as a firm requirement and even calls it "a primary success criterion," with no record that the brief's lighter-first-pass alternative was considered or explicitly confirmed. *Fix: add a line to §10 Assumptions Index or §9 Open Questions noting this was escalated from an open assumption to a confirmed decision (and briefly why).*

File written to: `D:\AI\NexusJobBmad\_bmad-output\planning-artifacts\prds\prd-NexusJobBmad-2026-09-05\reconcile-brief.md`
