import { toApiError } from '../lib'

import { AuthClient } from './nexus-api-client'

/**
 * Hand-written sibling of the generated client: the shared HTTP plumbing every
 * entities and features `api` segment needs — one module-scoped
 * `X-CSRF-TOKEN` cache, a `fetch` wrapper that adds `credentials: 'include'`
 * (always) and the token header (on mutating calls only), and the one-shot
 * antiforgery re-seed + retry.
 *
 * Moved verbatim from `entities/session/api` (`csrf.ts` plus the wrapper /
 * retry half of `authClient.ts`) when a second consumer, `entities/job-posting`,
 * needed it. The CSRF cache must be a single module-scoped instance — one token
 * per session — so it cannot be duplicated per slice; FSD forbids a clean
 * `entities/job-posting -> entities/session` import, so the plumbing moves down
 * a layer. Behaviour is identical to the pre-move code.
 *
 * This file sits inside `shared/api/` and imports `./nexus-api-client` — a
 * relative specifier that does not contain the string `shared/api`, so the
 * AD-16 `no-restricted-imports` gate does not flag it. Consumers reach
 * `createHttp` / `callWithCsrfRetry` through the `shared/api` barrel, which
 * the eslint override for their `api` segments already permits.
 */

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

/**
 * Put `credentials: 'include'` on every request and add the `X-CSRF-TOKEN`
 * header (read synchronously from the cache) on `POST`s only — GETs need no
 * token.
 */
export function withCredentialsAndCsrf(init?: RequestInit): RequestInit {
  const headers = new Headers(init?.headers)
  const method = (init?.method ?? 'GET').toUpperCase()
  if (method === 'POST') {
    const token = peekCsrfToken()
    if (token !== null) {
      headers.set('X-CSRF-TOKEN', token)
    }
  }
  return { ...init, credentials: 'include', headers }
}

/**
 * An antiforgery rejection is a `400` ProblemDetails whose title names it
 * (`AntiforgeryEndpointFilter` -> `Results.Problem(title: "Antiforgery token
 * validation failed.", statusCode: 400)`) and which carries no `errors` map (a
 * DataAnnotations `400` always does). Both conditions are required so an
 * unrelated bodiless `400` never triggers a re-seed + retry of a non-idempotent
 * POST.
 */
export function isAntiforgeryFailure(err: unknown): boolean {
  const apiError = toApiError(err)
  return (
    apiError?.status === 400 &&
    apiError.errors === undefined &&
    /antiforgery/i.test(apiError.title ?? '')
  )
}

/**
 * Seed the token, run the call, and on an antiforgery `400` clear the cache,
 * re-seed, and retry exactly once. The `fetch` wrapper only ever sees a
 * `Response`, so the retry cannot live there — it lives here where the thrown
 * ProblemDetails is visible.
 */
export async function callWithCsrfRetry<T>(call: () => Promise<T>): Promise<T> {
  await ensureCsrfToken()
  try {
    return await call()
  } catch (err) {
    if (!isAntiforgeryFailure(err)) {
      throw err
    }
    resetCsrfToken()
    await ensureCsrfToken()
    return call()
  }
}

/**
 * The `http` object every generated client class takes in its constructor:
 * `new SomeClient('', createHttp())`. Wraps `window.fetch` so `credentials:
 * 'include'` and the `X-CSRF-TOKEN` header are applied to every call the client
 * makes.
 */
export function createHttp(): {
  fetch(url: RequestInfo, init?: RequestInit): Promise<Response>
} {
  return { fetch: (url, init) => window.fetch(url, withCredentialsAndCsrf(init)) }
}
