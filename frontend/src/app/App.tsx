import '@fontsource-variable/inter'
import { createBrowserRouter, RouterProvider, type RouteObject } from 'react-router'

import { HomePage, NotFoundPage } from '../pages'
import { Container } from '../shared/ui'
import { AppShell } from '../widgets'

import '../shared/tokens/tokens.css'
import '../shared/tokens/base.css'

/**
 * Route-level error boundary element. Rendered outside the shell (the shell
 * itself may be what threw), so it stands on its own inside a `Container`.
 */
export function RouteError() {
  return (
    <Container>
      <p role="alert">Something went wrong. Please reload the page.</p>
    </Container>
  )
}

/**
 * Router shape (story 1.2): one layout route (`AppShell`) owns the chrome; child
 * routes render into its `<Outlet/>`. `/` → `HomePage`; anything else →
 * `NotFoundPage`, still inside the shell. The Host serves `index.html` for every
 * non-`/api` path (story 1.1), so these client paths resolve on a hard refresh.
 *
 * `viewer` is fixed to the signed-out state here; story 1.3 supplies a role.
 * Exported so a test can mount the same tree with an in-memory router.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    element: <AppShell viewer={{ kind: 'anonymous' }} />,
    errorElement: <RouteError />,
    children: [
      { index: true, element: <HomePage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]

const router = createBrowserRouter(routes)

export function App() {
  return <RouterProvider router={router} />
}
