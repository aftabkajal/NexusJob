import type { Role } from '../ui'

/**
 * The auth field validators + server-field-error mapper, moved verbatim from
 * `features/auth/model/useAuthForm.ts` (Story 3.2) so a second slice
 * (`features/apply-to-posting`'s apply-gate) can use the identical rules
 * without duplicating them — mirrors this codebase's own precedent for
 * `shared/api/http.ts` and `toApiError`.
 */

// A deliberately permissive check — the server's `[EmailAddress]` rule is
// authoritative; this only catches the obviously malformed before a request.
export const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function validateName(value: string, role: Role): string | undefined {
  if (value.trim() !== '') return undefined
  return role === 'jobSeeker' ? 'Enter your full name.' : 'Enter your company name.'
}

export function validateEmail(value: string): string | undefined {
  const trimmed = value.trim()
  if (trimmed === '') return 'Enter your email address.'
  if (!EMAIL_PATTERN.test(trimmed)) return 'Enter a valid email address.'
  return undefined
}

export function validatePassword(value: string): string | undefined {
  if (value === '') return 'Enter your password.'
  if (value.length < 8) return 'Your password must be at least 8 characters.'
  return undefined
}

// Shared across `useAuthForm` and `useApplyGateForm` (as `FieldErrors`) so the
// two forms' error-state shape can't drift apart the way the validators
// themselves would have if left duplicated.
export type AuthFieldErrors = { name?: string; email?: string; password?: string }

/**
 * Map a server-side `errors` map (a rule the client check missed) onto the
 * `name` / `email` / `password` field slots by matching the key substring.
 * The generalised form of `useAuthForm`'s local `mapServerFieldErrors`.
 */
export function mapAuthFieldErrors(errors: Record<string, string[]>): AuthFieldErrors {
  const mapped: AuthFieldErrors = {}
  for (const [key, messages] of Object.entries(errors)) {
    const message = messages[0]
    if (!message) continue
    const lower = key.toLowerCase()
    if (lower.includes('name')) mapped.name = message
    else if (lower.includes('email')) mapped.email = message
    else if (lower.includes('password')) mapped.password = message
  }
  return mapped
}
