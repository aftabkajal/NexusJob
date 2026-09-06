import { Link } from 'react-router'

import styles from './NotFoundPage.module.css'

/**
 * Catch-all route, rendered inside the shell — never a blank screen for an
 * unknown client path.
 */
export function NotFoundPage() {
  return (
    <div className={styles.page}>
      <h1 className={styles.title}>This page is not available.</h1>
      <p className={styles.body}>
        <Link to="/" className={styles['home-link']}>
          Return to the home page.
        </Link>
      </p>
    </div>
  )
}
