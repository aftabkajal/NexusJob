import {
  AuthClient,
  type AuthAccountResponse,
  type LoginRequest,
  type RegisterRequest,
} from '../../../shared/api'

import { ensureCsrfToken, peekCsrfToken, resetCsrfToken } from './csrf'
import { toApiError } from './toApiError'

/**
 * The `entities/session/api` segment owns the generated client (AD-16); three
 * files in it import from `shared/api` — this one, `csrf.ts`, and
 * `toApiError.ts`. This module puts two layers around it:
 *
 * 1. An `http` wrapper on the `AuthClient` ctor that puts `credentials:
 *    'include'` on every call and adds the `X-CSRF-TOKEN` header (read
 *    synchronously from the cache) on `POST`s only — `register` / `login` /
 *    `logout`. `csrf` / `me` are GETs and need no token.
 * 2. A thin `{ register, login, me, logout }` object whose three mutating
 *    methods run through `callWithCsrfRetry`: the token is seeded before the
 *    call, and an antiforgery `400` clears the cache, re-seeds, and retries
 *    once. The `fetch` wrapper only ever sees a `Response`, so the retry cannot
 *    live there — it lives here where the thrown ProblemDetails is visible.
 */
function withCredentialsAndCsrf(init?: RequestInit): RequestInit {
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

const rawClient = new AuthClient('', {
  fetch: (url, init) => window.fetch(url, withCredentialsAndCsrf(init)),
})

/**
 * An antiforgery rejection is a `400` ProblemDetails whose title names it
 * (`AntiforgeryEndpointFilter` → `Results.Problem(title: "Antiforgery token
 * validation failed.", statusCode: 400)`) and which carries no `errors` map (a
 * DataAnnotations `400` always does). Both conditions are required so an
 * unrelated bodiless `400` never triggers a re-seed + retry of a non-idempotent
 * POST.
 */
function isAntiforgeryFailure(err: unknown): boolean {
  const apiError = toApiError(err)
  return (
    apiError?.status === 400 &&
    apiError.errors === undefined &&
    /antiforgery/i.test(apiError.title ?? '')
  )
}

async function callWithCsrfRetry<T>(call: () => Promise<T>): Promise<T> {
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

export const authClient = {
  register: (body: RegisterRequest): Promise<AuthAccountResponse> =>
    callWithCsrfRetry(() => rawClient.register(body)),
  login: (body: LoginRequest): Promise<AuthAccountResponse> =>
    callWithCsrfRetry(() => rawClient.login(body)),
  logout: (): Promise<void> => callWithCsrfRetry(() => rawClient.logout()),
  me: (): Promise<AuthAccountResponse> => rawClient.me(),
}

export type { AuthAccountResponse, LoginRequest, RegisterRequest }
