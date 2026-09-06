import { useQueryClient } from '@tanstack/react-query'
import { Outlet, useNavigate } from 'react-router'

import { authClient, sessionQueryKey, useSession } from '../../entities'
import { Container } from '../../shared/ui'

import styles from './AppShell.module.css'
import { NavBar, type Viewer } from './NavBar'

interface AppShellProps {
  /**
   * Test-only override. Production derives the viewer from `useSession()`;
   * unit tests that mount the shell without a `QueryClientProvider`-backed
   * session pass this instead.
   */
  viewer?: Viewer
}

/**
 * The layout route element: the shared chrome (nav bar) plus an `<Outlet/>` for
 * the active child route, held in the 1120px `Container`.
 *
 * The viewer comes from `GET /api/auth/me` via `useSession()` — while it is
 * pending (`data === undefined`) or resolves to `null`, the anonymous shell
 * renders, so there is no flash of a "Log out" affordance. `AppShell` owns the
 * log-out handler and passes it down, keeping `NavBar` presentational.
 */
export function AppShell({ viewer: viewerOverride }: AppShellProps) {
  const session = useSession()
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const viewer: Viewer =
    viewerOverride ??
    (session.data
      ? { kind: 'company', displayName: session.data.displayName }
      : { kind: 'anonymous' })

  const handleLogOut = async () => {
    try {
      await authClient.logout()
    } catch {
      // Best-effort: the cookie may already be gone, or the antiforgery retry
      // exhausted. Clear the client-side session regardless.
    }
    queryClient.setQueryData(sessionQueryKey, null)
    void queryClient.invalidateQueries({ queryKey: sessionQueryKey })
    navigate('/')
  }

  return (
    <div className={styles.shell}>
      <NavBar viewer={viewer} onLogOut={handleLogOut} />
      <main className={styles.main}>
        <Container>
          <Outlet />
        </Container>
      </main>
    </div>
  )
}
