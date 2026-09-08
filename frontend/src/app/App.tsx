import '@fontsource-variable/inter'
import { QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, RouterProvider, type RouteObject } from 'react-router'

import {
  HomePage,
  NotFoundPage,
  PostAJobPage,
  PostingDetailPage,
  SignInPage,
} from '../pages'
import { Container } from '../shared/ui'
import { AppShell } from '../widgets'

import { queryClient } from './queryClient'

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
 * Router shape: one layout route (`AppShell`) owns the chrome; child routes
 * render into its `<Outlet/>`. `/` → `HomePage`; `/sign-in` → `SignInPage`;
 * `/post-a-job` → `PostAJobPage` (guarded to a signed-in Company);
 * `/job-postings/:id` → `PostingDetailPage` (open to everyone); anything
 * else → `NotFoundPage`, still inside the shell. The Host serves
 * `index.html` for every non-`/api` path (story 1.1), so these client paths
 * resolve on a hard refresh.
 *
 * `AppShell` takes no `viewer` prop here — it derives the viewer from
 * `useSession()` (`GET /api/auth/me`). Exported so a test can mount the same
 * tree with an in-memory router.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    element: <AppShell />,
    errorElement: <RouteError />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'sign-in', element: <SignInPage /> },
      { path: 'post-a-job', element: <PostAJobPage /> },
      { path: 'job-postings/:id', element: <PostingDetailPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]

const router = createBrowserRouter(routes)

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}
