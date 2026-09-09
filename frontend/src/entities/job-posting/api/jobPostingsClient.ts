import {
  JobPostingsClient,
  callWithCsrfRetry,
  createHttp,
  type CreateJobPostingRequest,
  type JobPostingDetailResponse,
  type JobPostingResponse,
  type JobPostingSearchResultResponse,
  type PageOfJobPostingSearchResultResponse,
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
  /**
   * Read one posting's public detail. A `GET`, so no `callWithCsrfRetry`
   * (mirroring `authClient.me`) — a `404` for a missing / mistyped id, or any
   * other failure, propagates to the caller unchanged.
   */
  getById: (id: string): Promise<JobPostingDetailResponse> => rawClient.getById(id),
  /**
   * Keyword search / browse-all over open postings. A `GET`, so no
   * `callWithCsrfRetry`. The generated `JobPostingsClient.search`'s `page` /
   * `pageSize` params are typed `any` — NSwag couldn't infer `integer` from the
   * OpenAPI schema for a plain `int` property (a known, deferred, pre-existing
   * gap; see `deferred-work.md`). This wrapper re-types both as `number` so
   * every caller in the app gets real typing without touching the generated
   * file.
   */
  search: (
    query: string,
    page: number,
    pageSize: number,
  ): Promise<PageOfJobPostingSearchResultResponse> => rawClient.search(query, page, pageSize),
}

export type {
  CreateJobPostingRequest,
  JobPostingDetailResponse,
  JobPostingResponse,
  JobPostingSearchResultResponse,
  PageOfJobPostingSearchResultResponse,
}
