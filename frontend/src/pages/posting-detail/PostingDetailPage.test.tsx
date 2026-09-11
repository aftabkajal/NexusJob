import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor, within } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { jobPostingsClient } from '../../entities/job-posting/api/jobPostingsClient'

import { PostingDetailPage } from './PostingDetailPage'

vi.mock('../../entities/job-posting/api/jobPostingsClient', () => ({
  jobPostingsClient: { getById: vi.fn() },
}))

// The posting-detail success render now hosts `<ApplyButton>` (Story 3.1b),
// which reads `useSession` + `useMyApplication` from the `entities` barrel.
// `useJobPosting` stays real — it resolves against the deep-mocked
// `jobPostingsClient` above (via `importOriginal`).
const useSessionMock = vi.fn()
const useMyApplicationMock = vi.fn()

vi.mock('../../entities', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../entities')>()
  return {
    ...actual,
    useSession: () => useSessionMock(),
    useMyApplication: (jobPostingId: string, enabled: boolean) =>
      useMyApplicationMock(jobPostingId, enabled),
    applicationsClient: { apply: vi.fn(), getMine: vi.fn() },
  }
})

const getById = vi.mocked(jobPostingsClient.getById)

const jobSeekerSession = {
  isPending: false,
  isError: false,
  data: { kind: 'jobSeeker', id: 'js-1', displayName: 'Amara' },
}

function renderAt(path: string, routePath = '/job-postings/:id') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: routePath, element: <PostingDetailPage /> },
      { path: '/', element: <div>home-landing</div> },
    ],
    { initialEntries: [path] },
  )
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

beforeEach(() => {
  getById.mockReset()
  // Default: signed-out visitor — `ApplyButton` renders as the apply-gate
  // trigger (Story 3.2), not hidden; the pre-3.1b/3.1b assertions below don't
  // click it, so they stay unaffected by the gate's own behavior.
  useSessionMock.mockReturnValue({ isPending: false, isError: false, data: null })
  useMyApplicationMock.mockReturnValue({ isPending: false, isError: false, data: undefined })
})

describe('PostingDetailPage', () => {
  it('redirects to "/" when the route has no id param (defensive guard)', () => {
    const router = renderAt('/job-postings', '/job-postings')

    expect(router.state.location.pathname).toBe('/')
    expect(screen.getByText('home-landing')).toBeInTheDocument()
    expect(getById).not.toHaveBeenCalled()
  })

  it('shows a role="status" skeleton while the query is pending', () => {
    getById.mockReturnValue(new Promise(() => {}))
    renderAt('/job-postings/jp-1')

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.getByText('Loading the job posting.')).toBeInTheDocument()
  })

  it('renders the title heading, company name, and description on success', async () => {
    getById.mockResolvedValue({
      id: 'jp-1',
      title: 'Staff Engineer',
      description: 'Build the platform.\nSecond line.',
      companyName: 'Cobalt Ledger',
    })
    renderAt('/job-postings/jp-1')

    const heading = await screen.findByRole('heading', { level: 1, name: 'Staff Engineer' })
    expect(heading).toBeInTheDocument()
    expect(screen.getByText('Cobalt Ledger')).toBeInTheDocument()
    expect(screen.getByText(/Build the platform\./)).toBeInTheDocument()
    expect(getById).toHaveBeenCalledWith('jp-1')
    // Focus lands on the title so a keyboard user arriving via the publish
    // redirect (or a direct link) is not stranded on <body>.
    await waitFor(() => expect(heading).toHaveFocus())
  })

  it('shows the not-available copy and a link to "/" on a 404', async () => {
    getById.mockRejectedValue({ status: 404, title: 'Not Found' })
    renderAt('/job-postings/missing')

    expect(
      await screen.findByText('This posting is no longer available.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to search' })).toHaveAttribute('href', '/')
  })

  it('shows the load-failed copy and the same link on a non-404 failure', async () => {
    getById.mockRejectedValue({ status: 500, title: 'Server error' })
    renderAt('/job-postings/jp-1')

    expect(
      await screen.findByText("We couldn't load this posting. Please try again."),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to search' })).toHaveAttribute('href', '/')
  })
})

describe('PostingDetailPage — apply surface (Story 3.1b)', () => {
  const posting = {
    id: 'jp-1',
    title: 'Staff Engineer',
    description: 'Build the platform.',
    companyName: 'Cobalt Ledger',
  }

  it('renders the Apply button in the success article for a signed-in Job Seeker', async () => {
    useSessionMock.mockReturnValue(jobSeekerSession)
    useMyApplicationMock.mockReturnValue({
      isPending: false,
      isError: false,
      data: { applied: false, appliedAt: undefined },
    })
    getById.mockResolvedValue(posting)
    renderAt('/job-postings/jp-1')

    const heading = await screen.findByRole('heading', { level: 1, name: 'Staff Engineer' })
    const article = heading.closest('article')
    expect(article).not.toBeNull()
    expect(
      within(article as HTMLElement).getByRole('button', { name: 'Apply' }),
    ).toBeInTheDocument()
    // The page passes its resolved posting id straight through, and gates the
    // `mine` query on the resolved Job Seeker session.
    expect(useMyApplicationMock).toHaveBeenCalledWith('jp-1', true)
    // ApplyButton never grabs focus on mount — the settle-focus effect still
    // lands on <h1>, even with the button now rendered in the same article.
    await waitFor(() => expect(heading).toHaveFocus())
  })

  it('renders no Apply button for a signed-in Company', async () => {
    useSessionMock.mockReturnValue({
      isPending: false,
      isError: false,
      data: { kind: 'company', id: 'co-1', displayName: 'Cobalt Ledger' },
    })
    getById.mockResolvedValue(posting)
    renderAt('/job-postings/jp-1')

    await screen.findByRole('heading', { level: 1, name: 'Staff Engineer' })
    expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument()
  })

  it('renders the apply-gate trigger button (not hidden) for a signed-out visitor, with no modal until clicked', async () => {
    getById.mockResolvedValue(posting)
    renderAt('/job-postings/jp-1')

    await screen.findByRole('heading', { level: 1, name: 'Staff Engineer' })
    const button = screen.getByRole('button', { name: 'Apply' })
    expect(button).toHaveAttribute('aria-haspopup', 'dialog')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('renders no Apply button while the session is unresolved', async () => {
    useSessionMock.mockReturnValue({ isPending: true, isError: false, data: undefined })
    getById.mockResolvedValue(posting)
    renderAt('/job-postings/jp-1')

    await screen.findByRole('heading', { level: 1, name: 'Staff Engineer' })
    expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument()
  })

  it('renders no Apply button while the posting is still loading', () => {
    useSessionMock.mockReturnValue(jobSeekerSession)
    getById.mockReturnValue(new Promise(() => {}))
    renderAt('/job-postings/jp-1')

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument()
  })

  it('renders no Apply button on the 404 branch', async () => {
    useSessionMock.mockReturnValue(jobSeekerSession)
    getById.mockRejectedValue({ status: 404, title: 'Not Found' })
    renderAt('/job-postings/missing')

    await screen.findByText('This posting is no longer available.')
    expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument()
  })
})
