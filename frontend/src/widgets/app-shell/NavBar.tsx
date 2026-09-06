import { Link, NavLink } from 'react-router'

import { Container } from '../../shared/ui'

import styles from './NavBar.module.css'

/**
 * Who is viewing the shell. Widened by story 1.3b to carry the signed-in
 * Company (fed from `GET /api/auth/me`); the Job Seeker kind arrives in 1.4.
 * Nav-item selection is a function of `viewer.kind`, so the union widening
 * needs no reshaping here.
 */
export type Viewer = { kind: 'anonymous' } | { kind: 'company'; displayName: string }

interface NavItem {
  label: string
  to: string
}

/**
 * The nav-item set for a viewer. Only links the current viewer can actually use
 * are returned — never a dead or disabled item pointing at a role-restricted
 * surface (Post a Job, My Postings, My Applications), which is why a signed-in
 * Company sees only Search here.
 */
export function navItemsFor(viewer: Viewer): NavItem[] {
  switch (viewer.kind) {
    case 'anonymous':
      return [
        { label: 'Search', to: '/' },
        { label: 'Sign up / Log in', to: '/sign-in' },
      ]
    case 'company':
      return [{ label: 'Search', to: '/' }]
    default:
      return []
  }
}

interface NavBarProps {
  viewer: Viewer
  /** Invoked by the signed-in Company's "Log out" control. */
  onLogOut?: () => void
}

export function NavBar({ viewer, onLogOut }: NavBarProps) {
  const items = navItemsFor(viewer)
  return (
    <header className={styles.bar}>
      <Container className={styles.inner}>
        <Link to="/" className={styles.wordmark}>
          NexusJob
        </Link>
        <nav aria-label="Primary" className={styles.nav}>
          {items.map((item) => (
            <NavLink key={item.to} to={item.to} end className={styles.link}>
              {item.label}
            </NavLink>
          ))}
          {viewer.kind === 'company' && (
            <>
              <span className={styles.account}>{viewer.displayName}</span>
              <button type="button" className={styles.logout} onClick={onLogOut}>
                Log out
              </button>
            </>
          )}
        </nav>
      </Container>
    </header>
  )
}
