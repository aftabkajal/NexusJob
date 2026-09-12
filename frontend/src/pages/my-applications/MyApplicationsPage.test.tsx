import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { applicationsClient } from '../../entities/application/api/applicationsClient'

import { MyApplicationsPage } from './MyApplicationsPage'

const useSessionMock = vi.fn()

vi.mock('../../entities/application/api/applicationsClient', () => ({
  applicationsClient: { apply: vi.fn(), getMine: vi.fn(), getMyApplications: vi.fn() },
}))

vi.mock('../../entities', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../entities')>()
  return {
    ...actual,
    useSession: () => useSessionMock(),
  }
})

const getMyApplications = vi.mocked(applicationsClient.getMyApplications)

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: '/my-applications', element: <MyApplicationsPage /> },
      { path: '/', element: <div>home-landing</div> },
    ],
    { initialEntries: ['/my-applications'] },
  )
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

function itemOf(applicationId: string, jobPostingId: string, jobPostingTitle: string, submittedAt: string) {
  return { applicationId, jobPostingId, jobPostingTitle, submittedAt }
}

beforeEach(() => {
  useSessionMock.mockReset()
  getMyApplications.mockReset()
})

describe('MyApplicationsPage guard', () => {
  it('renders nothing and does not redirect while the session is still pending', () => {
    useSessionMock.mockReturnValue({ isPending: true, isError: false, data: undefined })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/my-applications')
    expect(screen.queryByText('home-landing')).not.toBeInTheDocument()
    expect(getMyApplications).not.toHaveBeenCalled()
  })

  it('renders nothing and does not redirect when the session query has errored', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: true, data: undefined })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/my-applications')
    expect(getMyApplications).not.toHaveBeenCalled()
  })

  it('redirects to "/" once the session resolves to a Company', () => {
    useSessionMock.mockReturnValue({
      isPending: false,
      isError: false,
      data: { kind: 'company', id: 'c-1', displayName: 'Cobalt Ledger' },
    })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/')
    expect(screen.getByText('home-landing')).toBeInTheDocument()
    expect(getMyApplications).not.toHaveBeenCalled()
  })

  it('redirects to "/" for a signed-out visitor', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: false, data: null })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/')
    expect(getMyApplications).not.toHaveBeenCalled()
  })
})

describe('MyApplicationsPage list', () => {
  beforeEach(() => {
    useSessionMock.mockReturnValue({
      isPending: false,
      isError: false,
      data: { kind: 'jobSeeker', id: 'js-1', displayName: 'Priya Raman' },
    })
  })

  it('shows skeleton rows while the query is pending', () => {
    getMyApplications.mockReturnValue(new Promise(() => {}))
    renderPage()

    expect(getMyApplications).toHaveBeenCalledWith(1, 20)
    const results = screen.getByRole('region', { name: 'My applications' })
    expect(results.querySelectorAll('[aria-hidden="true"]')).toHaveLength(3)
  })

  it('shows the error banner on a rejected query, and retry re-issues the identical request', async () => {
    const user = userEvent.setup()
    getMyApplications.mockRejectedValueOnce({ status: 500, title: 'Server error' })
    renderPage()

    const banner = await screen.findByRole('alert')
    expect(banner).toHaveTextContent("We couldn't load your applications. Please try again.")
    expect(getMyApplications).toHaveBeenCalledTimes(1)

    getMyApplications.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 })
    await user.click(screen.getByRole('button', { name: 'Retry' }))

    await waitFor(() => expect(getMyApplications).toHaveBeenCalledTimes(2))
    expect(getMyApplications).toHaveBeenLastCalledWith(1, 20)
  })

  it('shows the empty-state copy and a link to Search when there are no applications', async () => {
    getMyApplications.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    renderPage()

    expect(
      await screen.findByText("You haven't applied to anything yet."),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Search' })).toHaveAttribute('href', '/')
  })

  it('renders one row per application, most-recent first as returned, linking to its posting', async () => {
    getMyApplications.mockResolvedValue({
      items: [
        itemOf('app-1', 'jp-1', 'Staff Engineer', '2026-09-11T00:00:00Z'),
        itemOf('app-2', 'jp-2', 'Product Manager', '2026-09-10T00:00:00Z'),
      ],
      page: 1,
      pageSize: 20,
      total: 2,
    })
    renderPage()

    expect(await screen.findByText('Staff Engineer')).toBeInTheDocument()
    expect(screen.getByText('Product Manager')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Staff Engineer/ })).toHaveAttribute(
      'href',
      '/job-postings/jp-1',
    )
    expect(screen.getByRole('link', { name: /Product Manager/ })).toHaveAttribute(
      'href',
      '/job-postings/jp-2',
    )
    expect(screen.getByText('Page 1 of 1')).toBeInTheDocument()
  })

  it('clicking Next re-runs the query with page: 2 and renders the new page', async () => {
    const user = userEvent.setup()
    getMyApplications
      .mockResolvedValueOnce({
        items: Array.from({ length: 20 }, (_, index) =>
          itemOf(`app-${index + 1}`, `jp-${index + 1}`, `Title ${index + 1}`, '2026-09-11T00:00:00Z'),
        ),
        page: 1,
        pageSize: 20,
        total: 25,
      })
      .mockResolvedValueOnce({
        items: [itemOf('app-21', 'jp-21', 'Title 21', '2026-09-01T00:00:00Z')],
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
    expect(screen.getByText('Title 21')).toBeInTheDocument()
    expect(getMyApplications).toHaveBeenLastCalledWith(2, 20)
  })
})
