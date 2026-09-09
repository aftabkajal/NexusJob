import { useQuery } from '@tanstack/react-query'

import { jobPostingsClient } from '../api/jobPostingsClient'

/**
 * The posting-detail query key, owned by this slice (AD-16): `['job-posting', id]`.
 */
export const jobPostingQueryKey = (id: string) => ['job-posting', id] as const

/**
 * Read one job posting's detail. Mirrors `useSession` in shape — a bare
 * `useQuery` with `staleTime: Infinity` (a v1 posting is immutable, so there is
 * nothing to refetch on mount or window refocus) and no local error catch. The
 * `queryFn` closes over `id`, so it cannot be module-scope. The `QueryClient`
 * default `retry: false` means a `404` (or any failure) fails on the first
 * attempt; the page branches on `toApiError(query.error)?.status`.
 */
export function useJobPosting(id: string) {
  return useQuery({
    queryKey: jobPostingQueryKey(id),
    queryFn: () => jobPostingsClient.getById(id),
    staleTime: Infinity,
  })
}

/**
 * The search / browse-all query key, owned by this slice (AD-16):
 * `['job-postings', 'search', query, page, pageSize]`.
 */
export const jobPostingSearchQueryKey = (query: string, page: number, pageSize: number) =>
  ['job-postings', 'search', query, page, pageSize] as const

/**
 * Keyword search / browse-all over open postings. Runs unconditionally — an
 * empty `query` is a real browse-all request (2.3a's "missing/empty query =
 * browse everything" semantics), not a gate to skip the query. No
 * `staleTime: Infinity` (unlike `useJobPosting`): new postings can appear
 * between searches, so each distinct key refetches fresh.
 */
export function useJobPostingSearch(query: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: jobPostingSearchQueryKey(query, page, pageSize),
    queryFn: () => jobPostingsClient.search(query, page, pageSize),
  })
}
