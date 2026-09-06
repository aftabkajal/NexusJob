import { Link, NavLink } from 'react-router'

import { Container } from '../../shared/ui'

import styles from './NavBar.module.css'

/**
 * Who is viewing the shell. Story 1.2 renders the signed-out state only; the
 * union is widened by story 1.3 to `{ kind: 'company' | 'jobSeeker'; ... }` and
 * fed from `GET /api/auth/me`. Nav-item selection is already a function of
 * `viewer.kind`, so that change needs no reshaping here.
 */
export type Viewer = { kind: 'anonymous' }

interface NavItem {
  label: string
  to: string
}

/**
 * The nav-item set for a viewer. Only links the current viewer can actually use
 * are returned — never a dead or disabled item pointing at a role-restricted
 * surface (Post a Job, My Postings, My Applications).
 */
export function navItemsFor(viewer: Viewer): NavItem[] {
  switch (viewer.kind) {
    case 'anonymous':
      return [
        { label: 'Search', to: '/' },
        { label: 'Sign up / Log in', to: '/sign-in' },
      ]
    default:
      return []
  }
}

interface NavBarProps {
  viewer: Viewer
}

export function NavBar({ viewer }: NavBarProps) {
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
        </nav>
      </Container>
    </header>
  )
}
