import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { applicationsClient } from '../../entities/application/api/applicationsClient'

import { ApplicantsPage } from './ApplicantsPage'

const useSessionMock = vi.fn()

vi.mock('../../entities/application/api/applicationsClient', () => ({
  applicationsClient: {
    apply: vi.fn(),
    getMine: vi.fn(),
    getMyApplications: vi.fn(),
    getApplicants: vi.fn(),
  },
}))

vi.mock('../../entities', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../entities')>()
  return {
    ...actual,
    useSession: () => useSessionMock(),
  }
})

const getApplicants = vi.mocked(applicationsClient.getApplicants)

function renderPage(
  path = '/my-postings/jp-1/applicants',
  routePath = '/my-postings/:id/applicants',
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: routePath, element: <ApplicantsPage /> },
      { path: '/my-postings', element: <div>my-postings-landing</div> },
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

function itemOf(jobSeekerId: string, fullName: string, email: string, submittedAt: string) {
  return { jobSeekerId, fullName, email, submittedAt }
}

beforeEach(() => {
  useSessionMock.mockReset()
  getApplicants.mockReset()
})

describe('ApplicantsPage — missing id param', () => {
  it('redirects to "/my-postings" when the route has no id param (defensive guard)', () => {
    const router = renderPage('/my-postings/applicants', '/my-postings/applicants')

    expect(router.state.location.pathname).toBe('/my-postings')
    expect(screen.getByText('my-postings-landing')).toBeInTheDocument()
    expect(getApplicants).not.toHaveBeenCalled()
  })
})

describe('ApplicantsPage guard', () => {
  it('renders nothing and does not redirect while the session is still pending', () => {
    useSessionMock.mockReturnValue({ isPending: true, isError: false, data: undefined })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/my-postings/jp-1/applicants')
    expect(screen.queryByText('home-landing')).not.toBeInTheDocument()
    expect(getApplicants).not.toHaveBeenCalled()
  })

  it('renders nothing and does not redirect when the session query has errored', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: true, data: undefined })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/my-postings/jp-1/applicants')
    expect(getApplicants).not.toHaveBeenCalled()
  })

  it('redirects to "/" once the session resolves to a Job Seeker', () => {
    useSessionMock.mockReturnValue({
      isPending: false,
      isError: false,
      data: { kind: 'jobSeeker', id: 'js-1', displayName: 'Priya Raman' },
    })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/')
    expect(screen.getByText('home-landing')).toBeInTheDocument()
    expect(getApplicants).not.toHaveBeenCalled()
  })

  it('redirects to "/" for a signed-out visitor', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: false, data: null })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/')
    expect(getApplicants).not.toHaveBeenCalled()
  })
})

describe('ApplicantsPage list', () => {
  beforeEach(() => {
    useSessionMock.mockReturnValue({
      isPending: false,
      isError: false,
      data: { kind: 'company', id: 'co-1', displayName: 'Cobalt Ledger' },
    })
  })

  it('renders a persistent "Back to My Postings" link', async () => {
    getApplicants.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    renderPage()

    const link = await screen.findByRole('link', { name: 'Back to My Postings' })
    expect(link).toHaveAttribute('href', '/my-postings')
  })

  it('shows skeleton rows while the query is pending', () => {
    getApplicants.mockReturnValue(new Promise(() => {}))
    renderPage()

    expect(getApplicants).toHaveBeenCalledWith('jp-1', 1, 20)
    const results = screen.getByRole('region', { name: 'Applicants' })
    expect(results.querySelectorAll('[aria-hidden="true"]')).toHaveLength(3)
    // The back link is present even while the list is loading.
    expect(screen.getByRole('link', { name: 'Back to My Postings' })).toBeInTheDocument()
  })

  it('shows "This posting is no longer available." on a 404, with no retry button', async () => {
    getApplicants.mockRejectedValue({ status: 404, title: 'Not Found' })
    renderPage()

    expect(
      await screen.findByText('This posting is no longer available.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to My Postings' })).toBeInTheDocument()
  })

  it('shows the generic error banner on a non-404 failure, and retry re-issues the identical request', async () => {
    const user = userEvent.setup()
    getApplicants.mockRejectedValueOnce({ status: 500, title: 'Server error' })
    renderPage()

    const banner = await screen.findByRole('alert')
    expect(banner).toHaveTextContent("We couldn't load the applicants. Please try again.")
    expect(getApplicants).toHaveBeenCalledTimes(1)

    getApplicants.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 })
    await user.click(screen.getByRole('button', { name: 'Retry' }))

    await waitFor(() => expect(getApplicants).toHaveBeenCalledTimes(2))
    expect(getApplicants).toHaveBeenLastCalledWith('jp-1', 1, 20)
  })

  it('shows the empty-state copy when there are no applicants', async () => {
    getApplicants.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    renderPage()

    expect(await screen.findByText('No applicants yet.')).toBeInTheDocument()
  })

  it('renders one read-only row per applicant, most-recent first as returned, with no controls', async () => {
    getApplicants.mockResolvedValue({
      items: [
        itemOf('js-1', 'Priya Raman', 'priya@example.com', '2026-09-11T00:00:00Z'),
        itemOf('js-2', 'Amara Okafor', 'amara@example.com', '2026-09-10T00:00:00Z'),
      ],
      page: 1,
      pageSize: 20,
      total: 2,
    })
    renderPage()

    expect(await screen.findByText('Priya Raman')).toBeInTheDocument()
    expect(screen.getByText('priya@example.com')).toBeInTheDocument()
    expect(screen.getByText('Amara Okafor')).toBeInTheDocument()
    expect(screen.getByText('amara@example.com')).toBeInTheDocument()
    expect(screen.getByText('Page 1 of 1')).toBeInTheDocument()
    expect(screen.queryAllByRole('button', { name: /view|remove|status/i })).toHaveLength(0)
  })

  it('clicking Next re-runs the query with page: 2 and renders the new page', async () => {
    const user = userEvent.setup()
    getApplicants
      .mockResolvedValueOnce({
        items: Array.from({ length: 20 }, (_, index) =>
          itemOf(
            `js-${index + 1}`,
            `Applicant ${index + 1}`,
            `applicant${index + 1}@example.com`,
            '2026-09-11T00:00:00Z',
          ),
        ),
        page: 1,
        pageSize: 20,
        total: 25,
      })
      .mockResolvedValueOnce({
        items: [itemOf('js-21', 'Applicant 21', 'applicant21@example.com', '2026-09-01T00:00:00Z')],
        page: 2,
        pageSize: 20,
        total: 25,
      })
    renderPage()

    await screen.findByText('Page 1 of 2')
    expect(screen.getByRole('button', { name: 'Prev' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()

    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Page 2 of 2')).toBeInTheDocument()
    expect(screen.getByText('Applicant 21')).toBeInTheDocument()
    expect(getApplicants).toHaveBeenLastCalledWith('jp-1', 2, 20)
  })
})
