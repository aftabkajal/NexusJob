import {
  AuthClient,
  callWithCsrfRetry,
  createHttp,
  type AuthAccountResponse,
  type LoginRequest,
  type RegisterRequest,
} from '../../../shared/api'

/**
 * The `entities/session/api` segment owns the generated client (AD-16). The
 * credentials / `X-CSRF-TOKEN` `fetch` plumbing and the one-shot antiforgery
 * retry now live in `shared/api/http.ts` (a second slice, `entities/job-posting`,
 * consumes them too) — this module just wires them onto an `AuthClient` and
 * exposes a thin `{ register, login, logout, me }` object.
 *
 * `createHttp()` puts `credentials: 'include'` on every call and the
 * `X-CSRF-TOKEN` header on `POST`s; the three mutating methods run through
 * `callWithCsrfRetry`, which seeds the token before the call and re-seeds +
 * retries once on an antiforgery `400`. `me` is a GET and needs neither.
 */
const rawClient = new AuthClient('', createHttp())

export const authClient = {
  register: (body: RegisterRequest): Promise<AuthAccountResponse> =>
    callWithCsrfRetry(() => rawClient.register(body)),
  login: (body: LoginRequest): Promise<AuthAccountResponse> =>
    callWithCsrfRetry(() => rawClient.login(body)),
  logout: (): Promise<void> => callWithCsrfRetry(() => rawClient.logout()),
  me: (): Promise<AuthAccountResponse> => rawClient.me(),
}

export type { AuthAccountResponse, LoginRequest, RegisterRequest }
