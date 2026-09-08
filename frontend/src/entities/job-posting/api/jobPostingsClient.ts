import {
  JobPostingsClient,
  callWithCsrfRetry,
  createHttp,
  type CreateJobPostingRequest,
  type JobPostingResponse,
} from '../../../shared/api'

/**
 * The `entities/job-posting/api` segment owns the generated `JobPostingsClient`
 * (AD-16) — the second consumer of the shared HTTP plumbing in
 * `shared/api/http.ts`.
 *
 * `createHttp()` puts `credentials: 'include'` on every call and the
 * `X-CSRF-TOKEN` header on the `POST`; `callWithCsrfRetry` seeds the token
 * before the call and re-seeds + retries once on an antiforgery `400`. Every
 * other failure (a validation `400` with an `errors` map, `401` / `403` for a
 * non-Company, a network error) propagates to the caller unchanged.
 */
const rawClient = new JobPostingsClient('', createHttp())

export const jobPostingsClient = {
  create: (body: CreateJobPostingRequest): Promise<JobPostingResponse> =>
    callWithCsrfRetry(() => rawClient.create(body)),
}

export type { CreateJobPostingRequest, JobPostingResponse }
