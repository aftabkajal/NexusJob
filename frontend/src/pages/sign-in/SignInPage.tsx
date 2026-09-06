import { Navigate } from 'react-router'

import { useSession } from '../../entities'
import { AuthForm } from '../../features'

import styles from './SignInPage.module.css'

/**
 * `/sign-in` — the sign-up / log-in screen, rendered into the shell's
 * `<Outlet/>` (already inside the 1120px `Container`). A signed-in Company has
 * no reason to re-authenticate, so it is redirected to `/`. While `me` is
 * pending, `session.data` is `undefined` and the form renders.
 */
export function SignInPage() {
  const session = useSession()

  if (session.data) {
    return <Navigate to="/" replace />
  }

  return (
    <div className={styles.screen}>
      <AuthForm />
    </div>
  )
}
