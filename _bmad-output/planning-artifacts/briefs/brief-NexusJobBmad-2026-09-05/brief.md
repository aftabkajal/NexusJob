---
title: Product Brief: NexusJob
status: draft
created: 2026-09-05
updated: 2026-09-05
---

# Product Brief: NexusJob

## Executive Summary

NexusJob is a two-sided job application platform: companies post jobs, job seekers search for them, view details, and apply. Functionally, that is the entire product — deliberately.

The point of this project is not the job board. It is a practice vehicle for learning AI-assisted, spec-driven software development: taking a system from idea through architecture through implementation using AI as a working partner at each stage, on a domain simple enough that the *process* stays the visible thing, not the feature list. The technical target is a genuine modular monolith built with Domain-Driven Design and Vertical Slice Architecture on a .NET backend, paired with a React frontend — structured, tested, and boundary-enforced the way a production system would be, not a tutorial CRUD app. [ASSUMPTION: "genuine" implies CI-enforced module boundaries and real bounded-context separation are in scope for v1, not deferred — confirm if this should be lighter for a first pass.]

This is not a resume artifact and not a business pitch. It is not competing for users or market share. Its success is measured by what the builder learns and by the quality of the reference implementation produced, not by adoption, revenue, or market differentiation.

## The Core Loop

The product itself is intentionally minimal — a boundary, not a backlog:

- A company creates a job posting.
- A job seeker searches job postings.
- A job seeker views the details of a specific posting.
- A job seeker applies to a posting.
- A company views the list of applicants for its posting. [ASSUMPTION: added because "apply" is meaningless without a recipient — a plain list, no scoring/filtering/ATS behavior.]

Both companies and job seekers need basic accounts to post or apply. [ASSUMPTION: standard signup/login per role; no SSO, no verification workflow, no admin approval of company accounts — flag if any of that is actually required.]

Nothing else is in v1. See Scope below for what is explicitly excluded.

## Why This Shape

A generic job board has no market story worth telling — LinkedIn, Indeed, and dozens of others already do this well, and NexusJob is not trying to out-compete them. That is fine, because the job board is not the deliverable. It is the smallest domain that still has two real actors, real state transitions (posted → viewed → applied), and enough natural structure to justify splitting into bounded contexts (e.g., Job Postings, Applications, Identity/Company) — which is exactly what the architecture exercise needs and what a to-do-list app would not provide.

## What Makes This Different

Nothing, deliberately — and that is the honest answer rather than a fabricated one. NexusJob does not aim to differentiate as a product. If there is a "different" here, it is in how it is built: an AI-assisted, spec-driven workflow driving the project from brief through architecture through implementation, applied to a real (if small) domain rather than a synthetic exercise.

## Who This Serves

**Primary: the builder (learning purpose).** Success here means gaining working fluency in AI-assisted spec-driven development and in applying DDD / modular-monolith / Vertical Slice Architecture concepts to a real, if small, system — not abstractly, but by having made the boundary decisions and lived with their consequences.

**Secondary: the two in-product user roles**, present because the practice domain needs them to be real:
- *Companies* — need to post a job and see who applied. Nothing more.
- *Job seekers* — need to find a job, see enough detail to decide, and apply. Nothing more.

Neither role represents a researched, validated user segment — they are minimal personas sized to what the core loop requires.

## Success Criteria

Since this is a learning project, success is defined by the process and the artifact, not by usage metrics:

- The core loop (post → search → view → apply → view applicants) works end-to-end, deployed and usable.
- Module/bounded-context boundaries are real and enforced (e.g., via architecture tests in CI), not just described in a diagram.
- The spec-driven workflow itself — brief → architecture → implementation — produces artifacts the builder can point to and explain, including the reasoning behind decisions (this brief and its addendum are the first of those artifacts).
- The builder can articulate, after the fact, what the modular-monolith/DDD/Vertical-Slice approach cost and bought them on a real (small) system — not just recite the theory.

## Scope

**In scope for v1:**
- Company: sign up / log in, create a job posting, view list of applicants for own posting(s).
- Job seeker: sign up / log in, search job postings, view a posting's details, apply to a posting.
- Backend built as a modular monolith per accepted architecture (see addendum for detail).
- Frontend built as a single web application per accepted architecture (see addendum for detail).

**Explicitly out of scope for v1:**
- Resume upload/parsing, AI-based matching or screening.
- Application status tracking/workflow beyond "applied" (no interview stages, offers, rejections).
- Messaging or notifications (email, in-app, or otherwise).
- Search filters beyond basic keyword search. [ASSUMPTION: confirm "search jobs" means simple keyword search, not faceted filtering by location/salary/etc.]
- Admin/moderation tooling, content review, or abuse handling.
- Mobile app (web-only, confirmed).
- Payments, subscriptions, or paid job listings.
- Multi-tenant company accounts (teams, roles within a company).

## What This Is Not

This brief deliberately omits sections a market-facing product brief would normally carry — competitive landscape, business model, go-to-market, market sizing — because none of them apply to a personal learning project with no users and no launch. Where a template asked for them, they have been dropped rather than filled with invented numbers or a fabricated moat.
