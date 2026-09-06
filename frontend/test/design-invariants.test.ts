import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

import { describe, expect, it } from 'vitest'

/**
 * Guards for the two I/O-matrix rows whose behaviour is CSS-only and so not
 * observable in jsdom — "Reduced motion" and "Narrow viewport". They assert the
 * mechanism is present in source, so a later edit can't quietly drop it. Lives
 * under `test/` (node tsconfig) because it reads the filesystem; the browser
 * `src/` tests never do.
 *
 * The "no width breakpoints" rule is also enforced live by
 * `media-feature-name-disallowed-list` in `.stylelintrc.json` (`npm run lint:tokens`).
 */

const srcRoot = join(import.meta.dirname, '..', 'src')

const cssFiles = readdirSync(srcRoot, { recursive: true, encoding: 'utf8' })
  .filter((entry) => entry.endsWith('.css'))
  .map((entry) => ({ path: entry, text: readFileSync(join(srcRoot, entry), 'utf8') }))

const read = (suffix: string) => {
  const hit = cssFiles.find((f) => f.path.replace(/\\/g, '/').endsWith(suffix))
  if (!hit) throw new Error(`no CSS file ending in ${suffix}`)
  return hit.text
}

describe('reduced-motion kill-switch (shared/tokens/base.css)', () => {
  const baseCss = read('shared/tokens/base.css')

  it('suppresses transitions and animations under prefers-reduced-motion: reduce', () => {
    expect(baseCss).toMatch(/@media\s*\(prefers-reduced-motion:\s*reduce\)/)
    expect(baseCss).toMatch(/transition-duration:\s*0\.01ms\s*!important/)
    expect(baseCss).toMatch(/animation-duration:\s*0\.01ms\s*!important/)
  })

  it('renders a visible keyboard focus indicator', () => {
    expect(baseCss).toMatch(/:focus-visible\s*\{[^}]*outline:/)
  })
})

describe('fixed-width layout, no responsive breakpoints', () => {
  it('the Container caps content at 1120px', () => {
    expect(read('shared/ui/Container.module.css')).toMatch(/max-width:\s*1120px/)
  })

  it('no stylesheet under src/ introduces a width breakpoint', () => {
    expect(cssFiles.length).toBeGreaterThan(0)
    // Catches legacy `(min-width` / `(max-width` and CSS range syntax
    // `(width >= …)`, `(width <= …)`, `(400px <= width …)`.
    const breakpoint = /@media[^{]*\(\s*(?:(?:min|max)-width\b|width\s*[<>]=?|[\d.]+\S*\s*[<>]=?\s*width\b)/i
    for (const { path, text } of cssFiles) {
      expect(text, path).not.toMatch(breakpoint)
    }
  })
})
