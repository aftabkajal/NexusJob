import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it, vi } from 'vitest'

import { authClient, sessionQueryKey, type SessionViewer } from '../entities'

import { routes } from './App'

// Keep the real `entities` surface (queries, keys, `toApiError`) but replace the
// network-touching client so a mounted `useSession()` never hits `window.fetch`.
vi.mock('../entities', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../entities')>()
  return {
    ...actual,
    authClient: {
      me: vi.fn().mockRejectedValue({ status: 401 }),
      register: vi.fn(),
      login: vi.fn(),
      logout: vi.fn().mockResolvedValue(undefined),
    },
  }
})

/**
 * Mounts the real route tree (from `App.tsx`) with an in-memory router and a
 * fresh `QueryClient` whose `session` query is pre-seeded, so the shell renders
 * its viewer synchronously without a request.
 */
function renderAt(path: string, session: SessionViewer | null = null) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  queryClient.setQueryData(sessionQueryKey, session)
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const view = render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { ...view, queryClient, invalidateSpy }
}

describe('App routing — anonymous viewer', () => {
  it('renders the shell and the Home landing surface at "/"', () => {
    renderAt('/')

    expect(screen.getByRole('link', { name: 'NexusJob' })).toHaveAttribute('href', '/')
    expect(screen.getByRole('navigation', { name: 'Primary' })).toBeInTheDocument()

    expect(
      screen.getByRole('heading', { name: 'Find your next role. Post your next hire.' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('search')).toBeInTheDocument()
    expect(
      screen.getByText('Browsing and searching do not require an account.'),
    ).toBeInTheDocument()
    expect(screen.getByText('No open postings yet. Check back soon.')).toBeInTheDocument()
  })

  it('renders the not-available surface inside the shell for an unknown path', () => {
    renderAt('/does-not-exist')

    expect(screen.getByRole('navigation', { name: 'Primary' })).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'This page is not available.' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Return to the home page.' })).toHaveAttribute(
      'href',
      '/',
    )
  })

  it('renders the auth card at "/sign-in" (not the not-found surface)', () => {
    renderAt('/sign-in')

    expect(screen.getByRole('heading', { name: 'Create your account' })).toBeInTheDocument()
    expect(screen.getByRole('radiogroup', { name: 'Select account type' })).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Company' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Job Seeker' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
    expect(screen.getByText('Job Seeker accounts are coming soon.')).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'This page is not available.' }),
    ).not.toBeInTheDocument()
  })
})

describe('App routing — signed-in Company viewer', () => {
  it('redirects "/sign-in" to "/" — no reason to re-authenticate', () => {
    renderAt('/sign-in', { kind: 'company', id: 'c-1', displayName: 'Cobalt Ledger' })

    expect(
      screen.getByRole('heading', { name: 'Find your next role. Post your next hire.' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Create your account' }),
    ).not.toBeInTheDocument()
  })

  it('shows the Company nav (Search + display name + Log out, no Epic-2 links)', () => {
    renderAt('/', { kind: 'company', id: 'c-1', displayName: 'Cobalt Ledger' })

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    expect(nav).toHaveTextContent('Search')
    expect(nav).toHaveTextContent('Cobalt Ledger')
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Post a Job' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'My Postings' })).not.toBeInTheDocument()
  })

  it('logs out: calls authClient.logout and invalidates the session query', async () => {
    const user = userEvent.setup()
    vi.mocked(authClient.logout).mockClear()
    const { invalidateSpy } = renderAt('/', {
      kind: 'company',
      id: 'c-1',
      displayName: 'Cobalt Ledger',
    })

    await user.click(screen.getByRole('button', { name: 'Log out' }))

    await waitFor(() => expect(authClient.logout).toHaveBeenCalledTimes(1))
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: sessionQueryKey })
  })

  it('still clears the session and returns to the anonymous shell when logout() rejects', async () => {
    const user = userEvent.setup()
    vi.mocked(authClient.logout).mockRejectedValueOnce({ status: 400 })
    const { queryClient } = renderAt('/', {
      kind: 'company',
      id: 'c-1',
      displayName: 'Cobalt Ledger',
    })

    // The rejected logout must not surface as an unhandled rejection from onClick.
    await user.click(screen.getByRole('button', { name: 'Log out' }))

    await waitFor(() =>
      expect(screen.getByRole('link', { name: 'Sign up / Log in' })).toBeInTheDocument(),
    )
    expect(queryClient.getQueryData(sessionQueryKey)).toBeNull()
    expect(screen.queryByRole('button', { name: 'Log out' })).not.toBeInTheDocument()
  })
})
