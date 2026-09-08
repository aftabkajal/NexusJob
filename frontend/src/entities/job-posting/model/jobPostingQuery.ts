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
