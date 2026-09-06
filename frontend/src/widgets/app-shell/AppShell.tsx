import { Outlet } from 'react-router'

import { Container } from '../../shared/ui'

import styles from './AppShell.module.css'
import { NavBar, type Viewer } from './NavBar'

interface AppShellProps {
  /**
   * Who is viewing. Story 1.2 only ever passes `{ kind: 'anonymous' }`; the
   * prop is the seam story 1.3 uses to supply a signed-in role without
   * reshaping the shell.
   */
  viewer: Viewer
}

/**
 * The layout route element: the shared chrome (nav bar) plus an `<Outlet/>` for
 * the active child route, held in the 1120px `Container`.
 */
export function AppShell({ viewer }: AppShellProps) {
  return (
    <div className={styles.shell}>
      <NavBar viewer={viewer} />
      <main className={styles.main}>
        <Container>
          <Outlet />
        </Container>
      </main>
    </div>
  )
}
