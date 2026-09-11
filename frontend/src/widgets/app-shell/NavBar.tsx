import { Link, NavLink } from 'react-router'

import { Container } from '../../shared/ui'

import styles from './NavBar.module.css'

/**
 * Who is viewing the shell — anonymous, or a signed-in Company / Job Seeker
 * (fed from `GET /api/auth/me`). Nav-item selection is a function of
 * `viewer.kind`, and both signed-in roles render the same signed-in chrome.
 */
export type Viewer =
  | { kind: 'anonymous' }
  | { kind: 'company'; displayName: string }
  | { kind: 'jobSeeker'; displayName: string }

interface NavItem {
  label: string
  to: string
}

/**
 * The nav-item set for a viewer. Only links the current viewer can actually use
 * are returned — never a dead or disabled item pointing at a role-restricted
 * surface. A signed-in Company sees "Post a Job" (Story 2.1b); a signed-in Job
 * Seeker sees "My Applications" (Story 3.3b). "My Postings" remains out until
 * its surface lands, and a Job Seeker or anonymous viewer never sees the
 * Company-only "Post a Job" item (and vice versa).
 */
export function navItemsFor(viewer: Viewer): NavItem[] {
  switch (viewer.kind) {
    case 'anonymous':
      return [
        { label: 'Search', to: '/' },
        { label: 'Sign up / Log in', to: '/sign-in' },
      ]
    case 'company':
      return [
        { label: 'Search', to: '/' },
        { label: 'Post a Job', to: '/post-a-job' },
      ]
    case 'jobSeeker':
      return [
        { label: 'Search', to: '/' },
        { label: 'My Applications', to: '/my-applications' },
      ]
    default:
      return []
  }
}

interface NavBarProps {
  viewer: Viewer
  /** Invoked by a signed-in viewer's "Log out" control. */
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
          {viewer.kind !== 'anonymous' && (
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
