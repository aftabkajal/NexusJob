/*
 * Typed mirror of `tokens.css` for the few places that need token values in
 * JavaScript (inline `style` props, computed values, tests). `tokens.css` on
 * `:root` is canonical; prefer `var(--…)` in CSS and reach for this module only
 * when a value must exist in JS.
 *
 * Values match DESIGN.md and `tokens.css` exactly.
 */

import type { CSSProperties } from 'react'

export const colors = {
  background: '#f5f6fb',
  surface: '#ffffff',
  primary: '#23215e',
  primaryForeground: '#ffffff',
  accent: '#0a7a90',
  accentForeground: '#ffffff',
  textPrimary: '#1a1b2e',
  textSecondary: '#5b5e78',
  border: '#dfe1f0',
  success: '#0b7a42',
  successSubtle: '#e3f6ea',
  danger: '#c42744',
  dangerSubtle: '#fbe4e8',
} as const

export const radius = {
  sm: '6px',
  md: '10px',
  lg: '16px',
  xl: '24px',
  full: '9999px',
} as const

export const space = {
  1: '4px',
  2: '8px',
  3: '12px',
  4: '16px',
  5: '24px',
  6: '32px',
  7: '48px',
  8: '64px',
} as const

export const gap = {
  gutter: '32px',
  editorial: '96px',
} as const

export const fontFamily =
  "'Inter Variable', Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif"

export type TypeRampStep =
  | 'display'
  | 'heading'
  | 'headingSm'
  | 'body'
  | 'bodySm'
  | 'label'
  | 'caption'

/**
 * The Inter type ramp as ready-to-spread inline-style objects. Each entry is a
 * complete text style (family, size, weight, line-height, tracking).
 */
export const typeRamp: Record<TypeRampStep, CSSProperties> = {
  display: {
    fontFamily,
    fontSize: '40px',
    fontWeight: 700,
    lineHeight: 1.15,
    letterSpacing: '-0.02em',
  },
  heading: {
    fontFamily,
    fontSize: '28px',
    fontWeight: 700,
    lineHeight: 1.2,
    letterSpacing: '-0.01em',
  },
  headingSm: {
    fontFamily,
    fontSize: '20px',
    fontWeight: 600,
    lineHeight: 1.3,
  },
  body: {
    fontFamily,
    fontSize: '16px',
    fontWeight: 400,
    lineHeight: 1.6,
  },
  bodySm: {
    fontFamily,
    fontSize: '14px',
    fontWeight: 400,
    lineHeight: 1.55,
  },
  label: {
    fontFamily,
    fontSize: '13px',
    fontWeight: 600,
    lineHeight: 1.4,
    letterSpacing: '0.04em',
  },
  caption: {
    fontFamily,
    fontSize: '12px',
    fontWeight: 500,
    lineHeight: 1.4,
  },
}

/** Look up one type-ramp step as an inline-style object. */
export function typeStyle(step: TypeRampStep): CSSProperties {
  return typeRamp[step]
}
