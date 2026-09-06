import { readFileSync } from 'node:fs'
import { join } from 'node:path'

import { describe, expect, it } from 'vitest'

import { colors, gap, radius, space } from '../src/shared/tokens/tokens.ts'

/**
 * `tokens.ts` hand-mirrors the `:root` custom properties in `tokens.css`, and
 * nothing else keeps the two in step. This asserts every colour / radius /
 * spacing / named-gap constant equals its `--*` counterpart, case-insensitively
 * (CSS hex is lower-case; the constants need not be).
 */

const rootBlock = (() => {
  const css = readFileSync(
    join(import.meta.dirname, '..', 'src', 'shared', 'tokens', 'tokens.css'),
    'utf8',
  )
  const start = css.indexOf(':root')
  if (start === -1) throw new Error('tokens.css has no :root block')
  return css.slice(start, css.indexOf('}', start))
})()

function cssVar(name: string): string {
  const match = rootBlock.match(new RegExp(`${name}\\s*:\\s*([^;]+);`))
  if (!match) throw new Error(`tokens.css has no ${name} declaration`)
  return match[1].trim()
}

const cases: Array<[string, string]> = [
  ['--color-background', colors.background],
  ['--color-surface', colors.surface],
  ['--color-primary', colors.primary],
  ['--color-primary-foreground', colors.primaryForeground],
  ['--color-accent', colors.accent],
  ['--color-accent-foreground', colors.accentForeground],
  ['--color-text-primary', colors.textPrimary],
  ['--color-text-secondary', colors.textSecondary],
  ['--color-border', colors.border],
  ['--color-success', colors.success],
  ['--color-success-subtle', colors.successSubtle],
  ['--color-danger', colors.danger],
  ['--color-danger-subtle', colors.dangerSubtle],
  ...Object.entries(radius).map(([key, value]): [string, string] => [`--radius-${key}`, value]),
  ...Object.entries(space).map(([key, value]): [string, string] => [`--space-${key}`, value]),
  ['--gap-gutter', gap.gutter],
  ['--gap-editorial', gap.editorial],
]

describe('tokens.ts mirrors tokens.css', () => {
  it.each(cases)('%s agrees with tokens.ts', (name, tsValue) => {
    expect(tsValue.toLowerCase()).toBe(cssVar(name).toLowerCase())
  })
})
