import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { authClient, sessionQueryKey, type SessionViewer } from '../entities'
import { jobPostingsClient } from '../entities/job-posting/api/jobPostingsClient'

import { routes } from './App'

// Keep the real `entities` surface (queries, keys, `useSession`, `useJobPosting`)
// but replace the network-touching clients so a mounted query never hits
// `window.fetch`. `jobPostingsClient` is mocked at its own module so the real
// `useJobPosting` queryFn (which imports it directly, not via the barrel) picks
// the mock up too.
vi.mock('../entities/job-posting/api/jobPostingsClient', () => ({
  jobPostingsClient: { create: vi.fn(), getById: vi.fn() },
}))

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

const create = vi.mocked(jobPostingsClient.create)
const getById = vi.mocked(jobPostingsClient.getById)

beforeEach(() => {
  create.mockReset()
  getById.mockReset()
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
  return { ...view, queryClient, invalidateSpy, router }
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
    const jobSeekerOption = screen.getByRole('radio', { name: 'Job Seeker' })
    expect(jobSeekerOption).not.toHaveAttribute('aria-disabled')
    expect(jobSeekerOption).not.toBeChecked()
    expect(screen.queryByText('Job Seeker accounts are coming soon.')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'This page is not available.' }),
    ).not.toBeInTheDocument()
  })
})

describe('App routing — signed-in Job Seeker viewer', () => {
  it('redirects "/sign-in" to "/" — no reason to re-authenticate', () => {
    renderAt('/sign-in', { kind: 'jobSeeker', id: 'js-1', displayName: 'Priya Raman' })

    expect(
      screen.getByRole('heading', { name: 'Find your next role. Post your next hire.' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('heading', { name: 'Create your account' }),
    ).not.toBeInTheDocument()
  })

  it('shows the signed-in nav (Search + display name + Log out, no role-only links)', () => {
    renderAt('/', { kind: 'jobSeeker', id: 'js-1', displayName: 'Priya Raman' })

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    expect(nav).toHaveTextContent('Search')
    expect(nav).toHaveTextContent('Priya Raman')
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Post a Job' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'My Postings' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'My Applications' })).not.toBeInTheDocument()
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

  it('shows the Company nav (Search + Post a Job + display name + Log out)', () => {
    renderAt('/', { kind: 'company', id: 'c-1', displayName: 'Cobalt Ledger' })

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    expect(nav).toHaveTextContent('Search')
    expect(screen.getByRole('link', { name: 'Post a Job' })).toHaveAttribute('href', '/post-a-job')
    expect(nav).toHaveTextContent('Cobalt Ledger')
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument()
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

describe('App routing — /post-a-job guard', () => {
  it('renders the Post-a-Job card for a signed-in Company', () => {
    renderAt('/post-a-job', { kind: 'company', id: 'c-1', displayName: 'Cobalt Ledger' })

    expect(screen.getByRole('heading', { name: 'Post a job' })).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toBeInTheDocument()
    expect(screen.getByLabelText('Description')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Publish' })).toHaveAttribute('type', 'submit')
  })

  it('redirects a signed-in Job Seeker at /post-a-job to "/"', () => {
    renderAt('/post-a-job', { kind: 'jobSeeker', id: 'js-1', displayName: 'Priya Raman' })

    expect(
      screen.getByRole('heading', { name: 'Find your next role. Post your next hire.' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Post a job' })).not.toBeInTheDocument()
  })

  it('redirects an anonymous viewer at /post-a-job to "/"', () => {
    renderAt('/post-a-job')

    expect(
      screen.getByRole('heading', { name: 'Find your next role. Post your next hire.' }),
    ).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Post a job' })).not.toBeInTheDocument()
  })

  it('navigates a Company to the new posting detail after a successful publish', async () => {
    const user = userEvent.setup()
    create.mockResolvedValue({
      id: 'jp-777',
      title: 'Staff Engineer',
      description: 'Build the platform.',
      createdAt: '2026-09-09T00:00:00Z',
    })
    getById.mockResolvedValue({
      id: 'jp-777',
      title: 'Staff Engineer',
      description: 'Build the platform.',
      companyName: 'Cobalt Ledger',
    })
    const { router } = renderAt('/post-a-job', {
      kind: 'company',
      id: 'c-1',
      displayName: 'Cobalt Ledger',
    })

    await user.type(screen.getByLabelText('Title'), 'Staff Engineer')
    await user.type(screen.getByLabelText('Description'), 'Build the platform.')
    await user.click(screen.getByRole('button', { name: 'Publish' }))

    await waitFor(() =>
      expect(router.state.location.pathname).toBe('/job-postings/jp-777'),
    )
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Staff Engineer' }),
    ).toBeInTheDocument()
    expect(screen.queryByText('Your job posting has been published.')).not.toBeInTheDocument()
  })
})

describe('App routing — /job-postings/:id detail surface', () => {
  it('renders the posting detail card for a mocked getById, open to an anonymous viewer', async () => {
    getById.mockResolvedValue({
      id: 'jp-1',
      title: 'Product Manager',
      description: 'Own the roadmap.',
      companyName: 'Cobalt Ledger',
    })
    renderAt('/job-postings/jp-1')

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Product Manager' }),
    ).toBeInTheDocument()
    expect(screen.getByText('Cobalt Ledger')).toBeInTheDocument()
    expect(screen.getByText('Own the roadmap.')).toBeInTheDocument()
    expect(getById).toHaveBeenCalledWith('jp-1')
  })

  it('shows "This posting is no longer available." for an unknown id', async () => {
    getById.mockRejectedValue({ status: 404, title: 'Not Found' })
    renderAt('/job-postings/does-not-exist')

    expect(
      await screen.findByText('This posting is no longer available.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to search' })).toHaveAttribute('href', '/')
  })
})
