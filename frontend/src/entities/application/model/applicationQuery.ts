import { useQuery } from '@tanstack/react-query'

import { applicationsClient } from '../api/applicationsClient'

/**
 * The `mine` query key, owned by this slice (AD-16 / AR-10):
 * `['application', 'mine', jobPostingId]`. Every action that changes the
 * applied-state (a successful `apply`) invalidates exactly this key.
 */
export const applicationMineQueryKey = (jobPostingId: string) =>
  ['application', 'mine', jobPostingId] as const

/**
 * Read the caller's applied-state for one posting. A bare `useQuery` with
 * `staleTime: Infinity` (the applied-state only changes on an action this app
 * takes, which invalidates the key explicitly). `enabled` gates it to a
 * signed-in Job Seeker — the endpoint is `401` / `403` for anyone else — so
 * `useMyApplication(id, false)` never fires the request. Branch on
 * `MyApplicationResponse.applied`, never on `appliedAt` presence (3.1a always
 * serialises `appliedAt`, `null` when not applied).
 */
export function useMyApplication(jobPostingId: string, enabled: boolean) {
  return useQuery({
    queryKey: applicationMineQueryKey(jobPostingId),
    queryFn: () => applicationsClient.getMine(jobPostingId),
    enabled,
    staleTime: Infinity,
  })
}
