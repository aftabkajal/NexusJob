import {
  ApplicationsClient,
  callWithCsrfRetry,
  createHttp,
  type ApplicationResponse,
  type CreateApplicationRequest,
  type MyApplicationResponse,
} from '../../../shared/api'

/**
 * The `entities/application/api` segment owns the generated `ApplicationsClient`
 * (AD-16) — the AR-10 bounded-context-mirror pilot slice for the backend
 * `Applications` module. Mirrors `entities/job-posting/api/jobPostingsClient.ts`
 * exactly: `new ApplicationsClient('', createHttp())`, the mutating call wrapped
 * in `callWithCsrfRetry`, the `GET` called directly.
 *
 * `createHttp()` puts `credentials: 'include'` on every call and the
 * `X-CSRF-TOKEN` header on the `POST`; `callWithCsrfRetry` seeds the token
 * before the call and re-seeds + retries once on an antiforgery `400`. Every
 * other failure (a `400` validation body, `401` / `403` for a caller that is
 * not a Job Seeker, a `404` for a missing posting, a network error) propagates
 * to the caller unchanged.
 */
const rawClient = new ApplicationsClient('', createHttp())

export const applicationsClient = {
  /**
   * Submit this Job Seeker's application to a posting. The generated method is
   * `create`; this wrapper names it `apply`. A `POST`, so it goes through
   * `callWithCsrfRetry`. 3.1a is idempotent — a repeat attempt returns `200`
   * with the existing row, never a `409`.
   */
  apply: (body: CreateApplicationRequest): Promise<ApplicationResponse> =>
    callWithCsrfRetry(() => rawClient.create(body)),
  /**
   * Read the caller's applied-state for one posting (`GET
   * /api/applications/mine?jobPostingId=`). A `GET`, so no `callWithCsrfRetry`
   * (mirroring `jobPostingsClient.getById`). The endpoint is `401` / `403` for
   * anyone but a signed-in Job Seeker, so the caller must gate this behind that
   * check; any failure propagates unchanged.
   */
  getMine: (jobPostingId: string): Promise<MyApplicationResponse> => rawClient.getMine(jobPostingId),
}

export type { ApplicationResponse, CreateApplicationRequest, MyApplicationResponse }
