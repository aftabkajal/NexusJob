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

/**
 * The `mine/list` query key, owned by this slice: `['application', 'mine',
 * 'list', page, pageSize]`.
 */
export const applicationsMineListQueryKey = (page: number, pageSize: number) =>
  ['application', 'mine', 'list', page, pageSize] as const

/**
 * Read a page of the signed-in Job Seeker's own applications. A bare
 * `useQuery`, no `staleTime: Infinity` (unlike `useMyApplication`) — the list
 * changes as the seeker applies to more postings, mirrors
 * `useJobPostingSearch`. No `enabled` gate: this query only ever mounts inside
 * `MyApplicationsPage`, which is itself already gated to a resolved Job Seeker
 * session before rendering the list.
 */
export function useMyApplications(page: number, pageSize: number) {
  return useQuery({
    queryKey: applicationsMineListQueryKey(page, pageSize),
    queryFn: () => applicationsClient.getMyApplications(page, pageSize),
  })
}

/**
 * The `applicants` query key, owned by this slice: `['application',
 * 'applicants', jobPostingId, page, pageSize]`.
 */
export const applicantsQueryKey = (jobPostingId: string, page: number, pageSize: number) =>
  ['application', 'applicants', jobPostingId, page, pageSize] as const

/**
 * Read a page of a posting's applicants. A bare `useQuery`, no `staleTime:
 * Infinity` (mirrors `useMyApplications`) — the list changes as more Job
 * Seekers apply. No `enabled` gate: this query only ever mounts inside
 * `ApplicantsPage`, which is itself already gated to a resolved Company
 * session before rendering the list; the endpoint's own `404` (missing or
 * not-owned posting) surfaces as a query error the page branches on via
 * `toApiError`.
 */
export function useApplicants(jobPostingId: string, page: number, pageSize: number) {
  return useQuery({
    queryKey: applicantsQueryKey(jobPostingId, page, pageSize),
    queryFn: () => applicationsClient.getApplicants(jobPostingId, page, pageSize),
  })
}
