import { Navigate } from 'react-router'

import { useSession } from '../../entities'
import { CreatePostingForm } from '../../features'

/**
 * `/post-a-job` — the Post-a-Job surface, rendered into the shell's `<Outlet/>`
 * (already inside the 1120px `Container`).
 *
 * Company-only: the "Post a Job" nav item is shown only to a signed-in Company,
 * so any other *resolved* viewer here (an anonymous visitor or a Job Seeker)
 * arrived via a typed or stale URL and is sent to `/` — the form never renders.
 * While `me` is still pending or has errored the page renders nothing rather
 * than redirect: `AppShell` and this page mount together on a cold load (hard
 * refresh, bookmark, typed URL), so redirecting on a not-yet-resolved session
 * would bounce a genuine Company off its own page. The endpoint's own `401` /
 * `403` (Story 2.1a) is the real enforcement; a session that goes stale between
 * load and submit falls to the failure banner.
 */
export function PostAJobPage() {
  const session = useSession()

  if (session.isPending || session.isError) {
    return null
  }

  if (session.data?.kind !== 'company') {
    return <Navigate to="/" replace />
  }

  return <CreatePostingForm />
}
