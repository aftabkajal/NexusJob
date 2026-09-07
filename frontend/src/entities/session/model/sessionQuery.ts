import { useQuery } from '@tanstack/react-query'

import { authClient } from '../api/authClient'
import { toApiError } from '../api/toApiError'

/**
 * The `me` query key, owned by this slice (AD-16). Every action that changes
 * the session (register / login / logout) invalidates exactly this key.
 */
export const sessionQueryKey = ['session', 'me'] as const

/**
 * The resolved viewer. `null` from the query means "no viewer" (signed out).
 * `kind` mirrors the account's `accountType` (`company` / `job_seeker`) as the
 * camelCase UI value. Named `SessionViewer` to stay distinct from the
 * `widgets/app-shell` `Viewer` union that the shell renders from.
 */
export interface SessionViewer {
  kind: 'company' | 'jobSeeker'
  id: string
  displayName: string
}

async function fetchSession(): Promise<SessionViewer | null> {
  try {
    const account = await authClient.me()
    switch (account.accountType) {
      case 'company':
        return { kind: 'company', id: account.id, displayName: account.displayName }
      case 'job_seeker':
        return { kind: 'jobSeeker', id: account.id, displayName: account.displayName }
      default:
        throw new Error(`unexpected accountType: ${account.accountType}`)
    }
  } catch (err) {
    if (toApiError(err)?.status === 401) {
      return null
    }
    throw err
  }
}

/**
 * Read the current session. `staleTime: Infinity` because the session only
 * changes on an action this app takes, and each of those invalidates the key
 * explicitly — without it, `me` would refetch on every mount and window
 * refocus. The `QueryClient` has `retry: false`, so a signed-out load resolves
 * to `null` in one request.
 */
export function useSession() {
  return useQuery({
    queryKey: sessionQueryKey,
    queryFn: fetchSession,
    staleTime: Infinity,
  })
}
