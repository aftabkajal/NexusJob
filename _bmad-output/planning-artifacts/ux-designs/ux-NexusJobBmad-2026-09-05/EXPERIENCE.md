---
name: NexusJob
status: final
updated: 2026-09-05
sources:
  - ../../prds/prd-NexusJobBmad-2026-09-05/prd.md
---

# NexusJob — Experience Spine

> Two-sided job application platform (Company + Job Seeker). Single shared shell, role-aware content and actions — not two separate app shells. Desktop-first single web app, not required to be responsive. Paired with `DESIGN.md` (Nexus Indigo).

## Foundation

One shared shell and navigation bar for both roles; what renders in it (nav items, primary actions) is role-aware based on whether the signed-in account is a Company or a Job Seeker, and browsing/search stay open to signed-out visitors. Built from scratch — no inherited UI-system tokens; `DESIGN.md` is the visual identity reference and this spine is the behavior. Desktop-first, light-mode-only, subtle-motion-only per `DESIGN.md` §Brand & Style. Accessibility floor is WCAG 2.1 AA throughout — see Accessibility Floor below.

## Information Architecture

| Surface | Reached from | Purpose |
|---|---|---|
| Home / Search landing | App load (root URL) | Entry point for everyone; keyword search bar plus a browse of open postings; sign-up/log-in link in the shell |
| Search results | Home search submit | Paginated list of Job Postings matching the keyword (title/description substring match) |
| Job posting detail | Search results row click | Full posting details (title, description, Company name); Apply action, with the inline apply-gate for signed-out visitors |
| Sign up / Log in (role toggle) | Shell "Sign up / Log in" link, or triggered inline as the apply-gate | Create or authenticate a Company or Job Seeker account via the role-toggle form. Sign-up collects a name (company/display name for Company, full name for Job Seeker) alongside email and password — this is the source of the "Company name" shown on postings and the "Applicant name" shown in the Applicants list. |
| Post-a-Job form | Company nav "Post a Job" (authenticated Company only) | Create a new Job Posting (title + description) |
| My Postings + Applicants | Company nav "My Postings" (authenticated Company only) | List the Company's own postings; drill into a posting's Applicants list |
| My Applications | Job Seeker nav "My Applications" (authenticated Job Seeker only) | List postings the signed-in Job Seeker has applied to |

The shell's nav renders only the links relevant to the signed-in role (or to signed-out visitors); there is no dead/disabled nav item pointing at a surface the current viewer can't use.

→ Composition reference: `mockups/key-home-search.html` (Home/Search landing, Search results), `mockups/key-posting-detail.html` (Job posting detail + apply-gate), `mockups/key-auth.html` (Sign up/Log in), `mockups/key-my-postings.html` (My Postings + Applicants). Post-a-Job and My Applications are spine-only — built from the tables in this document and `DESIGN.md` alone. This spine wins on conflict with any mock.

## Voice and Tone

Formal, professional microcopy — corporate-adjacent register, no lorem, no emoji, no exclamation-heavy startup voice, paired with the confident/modern visual register in `DESIGN.md`.

| Do | Don't |
|---|---|
| "Your job posting has been published." | "Woohoo! Your job is live! 🎉" |
| "Your application has been submitted." | "You're in! Good luck out there!" |
| "This email is already registered as a Company." | "Oops — looks like that email's taken." |
| "We couldn't submit your application. Please try again." | "Uh-oh, something broke on our end!" |
| "No postings match \"{keyword}.\" Try a different term." | "Nothing here! Try again? 🔍" |
| Complete sentences, terminal punctuation, no exclamation marks | Casual fragments, exclamation marks, emoji as punctuation |

## Component Patterns

Behavioral rules only. Visual specs live in `DESIGN.md.Components`.

| Component | Use | Behavioral rules |
|---|---|---|
| `{components.job-card}` | Search results, Home browse | Clicking anywhere on the card except the Apply button opens Job posting detail. Apply button behavior is defined by `apply-button` below. Hover applies the elevation defined in `DESIGN.md` (Elevation & Depth) as the sole hover affordance. |
| `{components.applicant-row}` | My Postings → Applicants view | Read-only — no click action, no per-row controls (no scoring/status per PRD scope). Rows sort by application timestamp, most recent first. |
| `{components.apply-button}` | Job posting detail, Post-a-Job (as "Publish") | Signed-out Job Seeker: click opens the `apply-gate-modal` over the current posting. Signed-in Job Seeker who hasn't applied: click submits the application inline (no navigation) and the button relabels to a disabled "Applied" state. Signed-in Job Seeker who already applied: the button renders directly in the disabled "Applied" state on load — a second click is structurally not possible, matching the PRD's one-application-per-pair rule. |
| `{components.auth-role-toggle}` | Sign up / Log in surface, apply-gate modal | Exactly one role active at a time; switching clears role-specific field errors but keeps entered email/password/name. Operable via `Tab` + arrow keys, `Enter`/`Space` to select. Inside the apply-gate modal the toggle is pre-set to Job Seeker and effectively fixed, since only a Job Seeker can apply — the Company option is visually present but not the point of entry there. Sign-up mode (not log-in) shows a third field above email: a required name field, labeled "Company name" or "Full name" depending on the active role toggle position. Log-in mode never shows the name field. |
| `{components.apply-gate-modal}` | Job posting detail (signed-out Apply click) | Opens as an interstitial over the same posting (no page navigation, no redirect). On successful account creation inside the modal, the modal closes, the pending application auto-submits against the posting the visitor was already viewing, and the posting detail updates to the applied/confirmation state — no second Apply click. See Interaction Primitives for open/close/focus behavior and Key Flows for the full sequence. |

## State Patterns

| State | Surface | Treatment |
|---|---|---|
| Cold load | Home / Search landing | Skeleton rows in place of the browse listing; resolves to real postings or the empty-catalog state below. |
| Empty catalog (no postings exist yet) | Home / Search landing | "No open postings yet. Check back soon." — no error styling, this is an expected early-life state. |
| Cold load | Search results | Skeleton `{components.job-card}` rows matching the eventual layout. |
| No matches | Search results | "No postings match \"{keyword}.\" Try a different term." No result suggestions — search stays simple. |
| Search/network error | Search results | "We couldn't run that search. Please try again." Retry re-submits the same keyword. |
| Cold load | Job posting detail | Skeleton for title/description/company block. |
| Posting not found | Job posting detail | "This posting is no longer available." with a link back to Search — covers a bad/stale link, since v1 has no edit/deactivate lifecycle but a posting could still be mistyped or the id invalid. |
| Apply-gate error: duplicate email | Apply-gate modal | Inline under the email field: "This email is already registered as a Job Seeker." Modal stays open; user can switch to logging in with that email instead. |
| Apply-gate error: submit/network failure | Apply-gate modal / Job posting detail | Account creation succeeds but the auto-submit application fails: modal closes (account exists), posting detail shows "We couldn't submit your application. Please try again." with a retry action — the user is not asked to re-enter credentials. |
| Validation error | Sign up / Log in, Post-a-Job | Inline, per-field, below the field; see Interaction Primitives for timing. |
| Registration error: duplicate email | Sign up / Log in | Inline under the email field, role-specific: "This email is already registered as a {Company/Job Seeker}." Toggle and other field values are retained. |
| Sign-in error: no match | Sign up / Log in | Generic inline error that does not reveal which field was wrong, per PRD FR-1/FR-2: "That email and password don't match. Please try again." |
| Publish success | Post-a-Job | Confirmation: "Your job posting has been published." Redirects to the new posting's detail view, now live and searchable. |
| Save/network failure | Post-a-Job | Form values are retained; inline banner: "We couldn't publish this posting. Please try again." Retry re-submits the same title/description. |
| Cold load | My Postings + Applicants | Skeleton `{components.job-card}` rows for postings; skeleton `{components.applicant-row}` rows once a posting is opened. |
| Empty | My Postings + Applicants | No postings yet: "You haven't posted a job yet." with a link to Post-a-Job. A posting with zero applicants: "No applicants yet." — distinct copy from the no-postings-at-all state. |
| Permission denied | My Postings + Applicants | A Company navigating directly to another Company's posting id in this view is not shown that posting's applicants — treated the same as "not found," never a "blocked" screen that confirms the posting exists. |
| Cold load | My Applications | Skeleton `{components.job-card}`-style rows. |
| Empty | My Applications | "You haven't applied to anything yet." with a link to Search. |

## Interaction Primitives

NexusJob is a simple form-and-click web app — a job board, not power-user software. There is no keyboard-shortcut surface to speak of; the primitives below cover the real interaction questions this product actually has.

- **Search is submit-to-search, not search-as-you-type.** A single keyword field plus an explicit Search action; results only refresh on submit. This matches the calm, deliberate editorial register and the simplicity of a case-insensitive substring match — there's no live-filtering experience to build for a match this basic.
- **Pagination, not infinite scroll**, on both Search results and the Applicants list. Comfortable/editorial density means a bounded, page-at-a-time listing, not an endless feed.
- **Form validation timing:** validate on blur (per field) and again on submit; never on every keystroke. Errors render inline below the field in `{colors.danger}` text (see `DESIGN.md`) and are associated with the field for assistive tech (see Accessibility Floor).
- **Apply-gate modal open/close:** opens centered over a dimmed posting detail (no navigation). Opening moves focus to the first form field. `Escape` closes the modal and returns focus to the Apply button that triggered it, with no application submitted and no data lost from the posting view. Clicking the scrim also closes it (same as `Escape`). While open, focus is trapped inside the modal (`Tab`/`Shift+Tab` cycle only through its contents).

## Accessibility Floor

WCAG 2.1 AA. Visual contrast values live in `DESIGN.md.Colors` (all load-bearing text/background pairs verified there); this section is the behavioral half.

- **Focus order** follows visual reading order on every surface, including inside the apply-gate modal.
- **Keyboard operability:** every action reachable by mouse (search submit, Apply, Publish, role toggle, modal close) is reachable and operable by keyboard alone. The `{components.auth-role-toggle}` is operable via `Tab` + arrow keys with `Enter`/`Space` to select, both on the standalone auth surface and inside the apply-gate modal.
- **Modal focus trap:** the apply-gate modal traps focus while open and restores focus to the triggering Apply button on close (`Escape` or scrim click), per Interaction Primitives above.
- **Form error announcement:** inline field errors are programmatically associated with their field (so a screen reader announces the error when the field receives focus or on submit) rather than relying on color alone.
- **Alt text policy:** any Company logo image gets the Company name as its alt text; purely decorative icons (e.g. a checkmark beside a confirmation string) are marked decorative and hidden from assistive tech since the adjacent text already carries the meaning — consistent with `DESIGN.md`'s "pair color with text, never color alone" rule.

## Key Flows

### Flow 1 — Posting a role and finding out who answered (Raj, hiring manager, Tuesday morning)

1. Raj opens NexusJob and clicks "Sign up" in the shell. The auth-role-toggle defaults to Job Seeker; he selects Company.
2. He enters his company name, a work email, and a password, and submits.
3. **Failure path:** the email is already registered as a Company (he'd signed up once before and forgot). Inline error: "This email is already registered as a Company." He switches to logging in with the same email instead of retrying sign-up.
4. Signed in, he lands on the shell with Company-specific nav and clicks "Post a Job."
5. He fills in a title and description and clicks Publish.
6. Confirmation: "Your job posting has been published." The posting is immediately searchable and viewable by Job Seekers — no review delay.
7. A few days later, Raj returns, logs in, and opens My Postings — his posting is listed.
8. He clicks into it to open its Applicants view.
9. **Climax:** the Applicants list shows real names, emails, and application timestamps — not a placeholder or an empty state. The loop he set in motion by publishing has produced actual people who want the job, and he can see them without any additional step.

### Flow 2 — Searching, applying, and confirming in one motion (Amara, job-hunting, weekday evening)

1. Amara, not signed in, opens NexusJob and types "product designer" into the search field on Home, then submits.
2. **Near-miss:** her first search, for a narrower title she used at her last job, returns "No postings match \"design ops lead.\" Try a different term." She broadens the term to "product designer" and gets real results.
3. She opens a posting that looks right from the results list, reading the full title, description, and Company name on Job posting detail.
4. She clicks Apply. Because she isn't signed in, the apply-gate modal opens inline over the same posting — no page navigation, no losing her place.
5. The modal's role toggle is set to Job Seeker; she enters her full name, email, and password, and submits.
6. The account is created; the modal closes.
7. **Climax:** without a second Apply click, her application auto-submits against the exact posting she was reading, and the posting detail she's still looking at now shows "Your application has been submitted." The entire arc — decide, confirm identity, apply — happened without ever leaving the posting.
8. Later, she opens My Applications and sees the posting listed.

**Failure path:** at step 6, the account creation succeeds but the auto-submit of the application fails (a transient network/save failure). The modal still closes since her account now exists, and the posting detail shows "We couldn't submit your application. Please try again." with a retry action — Amara is not asked to re-enter her credentials or sign up again; a single retry click resumes from where the failure happened.
