---
name: NexusJob
description: Minimal two-sided job application platform (Company + Job Seeker); a from-scratch, confident/modern visual system in the Nexus Indigo palette.
status: final
updated: 2026-09-05
sources:
  - ../../prds/prd-NexusJobBmad-2026-09-05/prd.md
colors:
  background: '#F5F6FB'
  surface: '#FFFFFF'
  primary: '#23215E'
  primary-foreground: '#FFFFFF'
  accent: '#0A7A90'
  accent-foreground: '#FFFFFF'
  text-primary: '#1A1B2E'
  text-secondary: '#5B5E78'
  border: '#DFE1F0'
  success: '#0B7A42'
  success-subtle: '#E3F6EA'
  danger: '#C42744'
  danger-subtle: '#FBE4E8'
typography:
  display:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"
    fontSize: 40px
    fontWeight: '700'
    lineHeight: '1.15'
    letterSpacing: -0.02em
  heading:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"
    fontSize: 28px
    fontWeight: '700'
    lineHeight: '1.2'
    letterSpacing: -0.01em
  heading-sm:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"
    fontSize: 20px
    fontWeight: '600'
    lineHeight: '1.3'
  body:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.6'
  body-sm:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.55'
  label:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"
    fontSize: 13px
    fontWeight: '600'
    lineHeight: '1.4'
    letterSpacing: 0.04em
  caption:
    fontFamily: "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"
    fontSize: 12px
    fontWeight: '500'
    lineHeight: '1.4'
rounded:
  sm: 6px
  md: 10px
  lg: 16px
  xl: 24px
  full: 9999px
spacing:
  '1': 4px
  '2': 8px
  '3': 12px
  '4': 16px
  '5': 24px
  '6': 32px
  '7': 48px
  '8': 64px
  gutter: 32px
  editorial-gap: 96px
components:
  job-card:
    background: '{colors.surface}'
    border: '{colors.border}'
    radius: '{rounded.lg}'
    padding: '{spacing.5}'
    title-typography: '{typography.heading-sm}'
    title-color: '{colors.text-primary}'
    meta-typography: '{typography.body-sm}'
    meta-color: '{colors.text-secondary}'
  applicant-row:
    background: '{colors.surface}'
    divider: '{colors.border}'
    padding-x: '{spacing.4}'
    padding-y: '{spacing.3}'
    name-typography: '{typography.body}'
    name-color: '{colors.text-primary}'
    timestamp-typography: '{typography.caption}'
    timestamp-color: '{colors.text-secondary}'
  apply-button:
    background: '{colors.accent}'
    foreground: '{colors.accent-foreground}'
    radius: '{rounded.md}'
    padding: '{spacing.3} {spacing.5}'
    label-typography: '{typography.label}'
    disabled-background: '{colors.border}'
    disabled-foreground: '{colors.text-secondary}'
  auth-role-toggle:
    active-background: '{colors.primary}'
    active-foreground: '{colors.primary-foreground}'
    inactive-foreground: '{colors.text-secondary}'
    track-background: '{colors.surface}'
    track-border: '{colors.border}'
    radius: '{rounded.full}'
  apply-gate-modal:
    background: '{colors.surface}'
    overlay: 'rgba(26, 27, 46, 0.55)'
    radius: '{rounded.xl}'
    padding: '{spacing.6}'
    title-typography: '{typography.heading}'
    title-color: '{colors.text-primary}'
---

## Brand & Style

NexusJob reads as a sharp, tech-forward tool, not a legacy HR system — confident and modern, with a cool electric edge (Nexus Indigo: deep indigo grounded by an electric cyan signal). The visual register is bold and high-contrast; the microcopy register is formal and professional. Those two registers are meant to sit together deliberately: a product that looks like a sharp new tool but talks like it takes the job-search transaction seriously — see `EXPERIENCE.md` §Voice and Tone for the full register. Built from scratch — no inherited component-library defaults — so every token below is this product's own contract, not a customization of someone else's system.

Density is comfortable/editorial: generous whitespace, card-based listings and applicant views rather than dense data tables. Motion is subtle and purposeful only — fade/slide on state change, nothing decorative. Light mode only for v1. Desktop-first, single web app; not required to be responsive.

## Colors

- **Background (`#F5F6FB`)** and **Surface (`#FFFFFF`)** — the calm, cool-white canvas. Background sits behind the page shell; Surface is every card, row, form, and modal panel raised on top of it.
- **Primary (`#23215E`)** — deep indigo. Used for the app-chrome header/nav, the wordmark, and the auth-role-toggle's active state. Not used for body text or large fills below the fold — it's a chrome/identity color, not a content color.
- **Accent (`#0A7A90`)** — electric cyan-teal, the single conversion color. Used exclusively for the primary action per surface: the Apply button, the Publish button, the primary submit in the auth form. *Adjusted from the Nexus Indigo reference swatch (`#0EA5C4`), which cleared only ≈2.9:1 against white button text — well short of AA. This token is a darkened cyan on the same hue, chosen so white text/icons on accent clear AA at every relevant background.*
- **Text Primary (`#1A1B2E`)** and **Text Secondary (`#5B5E78`)** — primary is body/heading copy; secondary is metadata (timestamps, company names, helper text).
- **Border (`#DFE1F0`)** — hairline dividers and card outlines only; never used to carry meaning.
- **Success (`#0B7A42`)** / **Success Subtle (`#E3F6EA`)** — confirmation text and the subtle tint behind confirmation banners. *Adjusted from the reference swatch (`#0F9D58`), which cleared only ≈3.2–3.5:1 as text — insufficient for AA body text. Darkened on the same hue.*
- **Danger (`#C42744`)** / **Danger Subtle (`#FBE4E8`)** — error text and the subtle tint behind error banners. *Adjusted from the reference swatch (`#D6304A`), which cleared ≈4.44:1 against `{colors.background}` — just under the 4.5:1 floor. Darkened on the same hue.*

**Contrast (WCAG 2.1 AA, normal text ≥ 4.5:1 / UI components & large text ≥ 3:1), computed against the tokens above:**

| Combination | Ratio | Result |
|---|---|---|
| `{colors.text-primary}` on `{colors.background}` | ≈15.7:1 | Pass |
| `{colors.text-primary}` on `{colors.surface}` | ≈16.9:1 | Pass |
| `{colors.text-secondary}` on `{colors.background}` | ≈5.9:1 | Pass |
| `{colors.text-secondary}` on `{colors.surface}` | ≈6.3:1 | Pass |
| `{colors.primary-foreground}` on `{colors.primary}` | ≈14.5:1 | Pass |
| `{colors.accent-foreground}` on `{colors.accent}` (post-adjustment) | ≈5.0:1 | Pass |
| `{colors.accent}` as text/icon on `{colors.surface}` | ≈5.0:1 | Pass |
| `{colors.success}` as text on `{colors.surface}` / `{colors.background}` (post-adjustment) | ≈5.4:1 / ≈5.0:1 | Pass |
| `{colors.danger}` as text on `{colors.surface}` / `{colors.background}` (post-adjustment) | ≈5.6:1 / ≈5.2:1 | Pass |

Avoid: introducing a third chromatic color beyond primary and accent; using accent for chrome or decoration; using success/danger as fills behind large areas rather than as text/icon + subtle-tint pairs.

## Typography

Modern grotesque, all-sans, one family (Inter) for every role — no serif accent, no separate display cut, consistent with the confident/modern register and editorial density (the type ramp itself does the work, not a mixed-family flourish).

- `{typography.display}` (40px/700) — the Home landing headline only.
- `{typography.heading}` (28px/700) — page-level titles: job posting detail title, "Post a Job," "My Postings," apply-gate modal title.
- `{typography.heading-sm}` (20px/600) — card-level titles: job-card title, section sub-headers.
- `{typography.body}` (16px/400) — job descriptions, form field values, applicant-row names.
- `{typography.body-sm}` (14px/400) — secondary copy: job-card meta line, form helper text.
- `{typography.label}` (13px/600, tracked +0.04em) — form labels, button labels, the auth-role-toggle's two options.
- `{typography.caption}` (12px/500) — timestamps (e.g., "Posted 2 days ago" and applicant submission dates).

## Layout & Spacing

Generous, editorial spacing scale (`{spacing.1}`–`{spacing.8}`, 4px base) with two named large gaps: `{spacing.gutter}` (32px, between columns/cards in a listing grid) and `{spacing.editorial-gap}` (96px, between major page sections — e.g. between the search hero and the results grid). Content sits in a fixed desktop-first max-width container (1120px); no responsive breakpoints are specified, per the single-web-app form factor.

Listings (search results, My Postings, My Applications) render as a single-column stack of cards at `{spacing.gutter}` apart — never a dense multi-column table. Applicant rows within a posting's Applicants view are a simple vertical list (`{components.applicant-row}`), not a table with sortable columns — the product intentionally has no ranking/filtering to expose.

## Elevation & Depth

Elevation is used sparingly, in service of "what's interactive" and "what's currently on top" — not as decoration.

- **Resting cards** (job-card, applicant-row): no shadow, `{colors.border}` hairline only.
- **Hover** (job-card only, since it's the clickable listing unit): a soft lift — `0 1px 2px rgba(26,27,46,0.06), 0 4px 12px rgba(26,27,46,0.06)`.
- **Apply-gate modal**: elevated well above the page — `0 20px 48px rgba(26,27,46,0.18)` — over a scrim (`{components.apply-gate-modal.overlay}`) that dims but doesn't fully obscure the posting detail behind it, reinforcing "you're still on this same posting."

## Shapes

A moderate rounding scale — sharp enough to read tech-forward and confident, soft enough to stay comfortable/editorial rather than clinical: `{rounded.sm}` (6px) for inputs, `{rounded.md}` (10px) for buttons, `{rounded.lg}` (16px) for cards, `{rounded.xl}` (24px) for the apply-gate modal panel. `{rounded.full}` is reserved for the auth-role-toggle pill — the one place a fully-rounded shape earns its place, as a clear two-option switch.

## Components

- **job-card** (`{components.job-card}`) — Surface panel, `{rounded.lg}` corners, `{spacing.5}` internal padding. Title in `{typography.heading-sm}` / `{colors.text-primary}`; company name and posted-date meta in `{typography.body-sm}` / `{colors.text-secondary}`; description excerpt in `{typography.body}`. One `apply-button` in the card footer.
- **applicant-row** (`{components.applicant-row}`) — Full-bleed row inside the Applicants panel, `{colors.border}` divider between rows, no card shell per row. Applicant name/email in `{typography.body}`; application timestamp right-aligned in `{typography.caption}` / `{colors.text-secondary}`. No action controls on the row — the surface has no scoring, filtering, or status workflow.
- **apply-button** (`{components.apply-button}`) — Solid `{colors.accent}` fill, `{colors.accent-foreground}` label in `{typography.label}`, `{rounded.md}` corners. Same visual component serves as the "Publish" submit on Post-a-Job — it is *the* primary-action button, singular per surface.
- **auth-role-toggle** (`{components.auth-role-toggle}`) — Two-option pill switch ("Company" / "Job Seeker") at the top of the single Sign up/Log in surface. Active option: `{colors.primary}` fill, `{colors.primary-foreground}` label. Inactive option: transparent, `{colors.text-secondary}` label. `{rounded.full}` track with `{colors.border}` outline.
- **apply-gate-modal** (`{components.apply-gate-modal}`) — Centered overlay panel, `{colors.surface}` background, `{rounded.xl}` corners, `{spacing.6}` padding, title in `{typography.heading}`. Contains the same auth form as the standalone Sign up/Log in surface (role pre-set to Job Seeker, toggle not needed here since only a Job Seeker can apply), rendered as an interstitial over a dimmed but still-visible job posting detail.

→ Composition reference: `mockups/key-posting-detail.html` (job-card, apply-button, apply-gate-modal), `mockups/key-home-search.html` (job-card listing), `mockups/key-auth.html` (auth-role-toggle), `mockups/key-my-postings.html` (job-card, applicant-row). This spine wins on conflict with any mock.

## Do's and Don'ts

| Do | Don't |
|---|---|
| Use `{colors.accent}` only for the single primary action per surface (Apply, Publish, auth submit) | Use accent for chrome, nav highlights, or decorative flourishes |
| Keep listings as single-column cards with `{spacing.gutter}`+ between them | Compress postings or applicants into a dense multi-column table |
| One all-sans family (Inter) across every type role | Introduce a serif or script accent "for warmth" |
| Subtle fade/slide transitions on state change only | Decorative animation, parallax, or attention-seeking motion |
| Pair success/danger color with a text label at AA contrast | Ship a color-only status signal with no text |
| Reserve `{rounded.full}` for the auth-role-toggle | Apply pill shapes to cards, buttons, or panels generally |
