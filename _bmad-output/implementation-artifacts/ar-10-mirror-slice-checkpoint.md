---
title: 'AR-10 checkpoint — the frontend "bounded-context mirror" slice'
type: 'assessment'
created: '2026-09-11'
story: '3.1b'
status: 'complete'
---

# AR-10 checkpoint: did the bounded-context mirror slice pay off?

**AR-10** is the deliberate pilot of mapping one frontend FSD slice onto one
backend bounded context, applied only to the **Applications** capability, with
this written checkpoint required before the pattern spreads to any other slice
(epic-3-context, *Technical Decisions*). Story 3.1b is the pilot: it added
`entities/application` (the configured `ApplicationsClient` + the `mine`
applied-state query) and `features/apply-to-posting` (the `useMutation` + the
three-state `ApplyButton`) to mirror the backend `Applications` module end to
end.

## What the mapping cost

- **An extra `entities` slice that cannot import its siblings.** FSD forbids
  `entities/application` importing `entities/session` or `entities/job-posting`.
  `entities/application` therefore holds only what depends on nothing but
  `shared/` — the client wrapper, the three request/response types, the `mine`
  query key, and `useMyApplication(jobPostingId, enabled)`. The `enabled` flag is
  a parameter precisely because the slice cannot read `useSession()` itself; the
  caller must pass "is this a signed-in Job Seeker" in.
- **`features/` is forced to host every cross-entity composition.** The
  `ApplyButton`'s render depends on the session *kind* (an `entities/session`
  concern) and the posting id (an `entities/job-posting` concern), so the
  component and its `useApplyToPosting` hook must sit in `features/`, not in
  `entities/application/ui`. Anything the Applications capability needs that also
  touches identity or postings — the applicant list resolving names, the
  apply-gate chaining register-then-apply — will likewise land in `features/` or
  a `widget/`, never in the mirror `entities` slice. The mirror does not
  eliminate composition; it relocates it up a layer.
- **Two barrels and two test setups per capability** instead of one. The slice
  boundary is real work: `entities/application/index.ts`, the flat
  `entities/index.ts` re-export, `features/apply-to-posting/index.ts`,
  `features/index.ts`, plus mirrored `api` and `model` test files.

## What the mapping bought

- **Unambiguous ownership of the `mine` query key.** `applicationMineQueryKey`
  lives in `entities/application/model` and nowhere else; the feature's
  `useMutation.onSuccess` invalidates it by importing the factory, not by
  re-declaring the tuple. No other slice can mint a colliding
  `['application', 'mine', …]` key.
- **The generated `ApplicationsClient` never leaks past
  `entities/application/api`.** The AD-16 `no-restricted-imports` rule already
  enforces "generated client only inside an `*/api` segment"; the mirror slice
  makes the segment obvious and single-purpose. `model/`, `ui/`, and the page
  reach the client only through the slice barrel, exactly as
  `entities/job-posting` does.
- **The backend module boundary is legible from the frontend tree.** A reader
  who knows the backend has `Identity` / `JobPostings` / `Applications` modules
  can predict where the apply client and the `mine` query live. The slice name
  matches the module name.
- **It mirrors `entities/job-posting` shape-for-shape** (client wrapper +
  query-key factory + `useQuery` hook + barrel), so there was no new pattern to
  learn — the cost above is structural, not conceptual.

## Recommendation

**Keep the mirror for `entities/application` (it is done and it is clean), but do
not adopt "one slice per backend bounded context" as a general rule.** The value
delivered here — key ownership and client encapsulation — is delivered just as
well by the rules the codebase *already* enforces (AD-16 `no-restricted-imports`,
query keys owned by the noun slice) and by simply following the
`entities/job-posting` template. The distinctly "mirror" contribution is the
naming alignment, which is a documentation nicety, not an architectural
constraint worth a standing rule.

The real cost to watch is the second bullet above: the more a capability needs
cross-entity data, the more the mirror `entities` slice thins out to a
types-and-one-query husk while the actual logic accretes in `features/`. For
Applications that husk is acceptable. Before mirroring a *third* capability,
check whether its `entities` slice would hold anything that a plain
`features/<verb>` slice plus the existing key-ownership convention would not —
if not, skip the extra slice.

**Concrete guidance for the rest of Epic 3:**

- 3.2 (apply-gate): the modal composes `entities/session` (register) +
  `features/apply-to-posting` (the pending apply). It belongs in `features/` or a
  `widget/`, not in `entities/application`. Reuse `useApplyToPosting`'s mutation;
  do not add a second apply path.
- 3.3 (My Applications) / 3.4 (Applicants list): the list *queries* and their
  keys can live in `entities/application/model` (they depend only on `shared/`).
  The name/email resolution and the ownership check are cross-module reads —
  keep them in the page/feature that renders the list, batched once per page as
  epic-3-context requires.
