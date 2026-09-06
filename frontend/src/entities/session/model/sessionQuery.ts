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
 * Story 1.3b only knows the Company shape; the Job Seeker kind arrives in 1.4.
 * Named `SessionViewer` to stay distinct from the `widgets/app-shell` `Viewer`
 * union that the shell renders from.
 */
export interface SessionViewer {
  kind: 'company'
  id: string
  displayName: string
}

async function fetchSession(): Promise<SessionViewer | null> {
  try {
    const account = await authClient.me()
    return { kind: 'company', id: account.id, displayName: account.displayName }
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
