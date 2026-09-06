import { AuthClient } from '../../../shared/api'

/**
 * Lazy `X-CSRF-TOKEN` cache. `GET /api/auth/csrf` returns `{ token }` and sets
 * the antiforgery cookie; the token is fetched once, held in module scope, and
 * replayed on every mutating call. `resetCsrfToken()` clears it so the next
 * mutating call re-seeds (used by the antiforgery one-shot retry).
 */
let cachedToken: string | null = null

// A bare client for the CSRF GET only. `credentials: 'include'` so the
// antiforgery cookie the response sets is stored and replayed; no token header
// is needed on a GET.
const csrfClient = new AuthClient('', {
  fetch: (url, init) => window.fetch(url, { ...init, credentials: 'include' }),
})

/** The cached token, or `null` if it has not been fetched yet. Synchronous. */
export function peekCsrfToken(): string | null {
  return cachedToken
}

/** Fetch-and-cache the token if absent, then return it. */
export async function ensureCsrfToken(): Promise<string> {
  if (cachedToken === null) {
    const response = await csrfClient.csrf()
    cachedToken = response.token
  }
  return cachedToken
}

/** Drop the cached token so the next `ensureCsrfToken()` re-fetches. */
export function resetCsrfToken(): void {
  cachedToken = null
}
