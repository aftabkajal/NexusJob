import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { jobPostingsClient } from '../../entities/job-posting/api/jobPostingsClient'

import { MyPostingsPage } from './MyPostingsPage'

const useSessionMock = vi.fn()

vi.mock('../../entities/job-posting/api/jobPostingsClient', () => ({
  jobPostingsClient: { create: vi.fn(), getById: vi.fn(), search: vi.fn(), getMine: vi.fn() },
}))

vi.mock('../../entities', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../entities')>()
  return {
    ...actual,
    useSession: () => useSessionMock(),
  }
})

const getMine = vi.mocked(jobPostingsClient.getMine)

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: '/my-postings', element: <MyPostingsPage /> },
      { path: '/', element: <div>home-landing</div> },
      { path: '/post-a-job', element: <div>post-a-job-landing</div> },
    ],
    { initialEntries: ['/my-postings'] },
  )
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

function itemOf(id: string, title: string, createdAt: string) {
  return { id, title, description: 'A description.', createdAt }
}

beforeEach(() => {
  useSessionMock.mockReset()
  getMine.mockReset()
})

describe('MyPostingsPage guard', () => {
  it('renders nothing and does not redirect while the session is still pending', () => {
    useSessionMock.mockReturnValue({ isPending: true, isError: false, data: undefined })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/my-postings')
    expect(screen.queryByText('home-landing')).not.toBeInTheDocument()
    expect(getMine).not.toHaveBeenCalled()
  })

  it('renders nothing and does not redirect when the session query has errored', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: true, data: undefined })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/my-postings')
    expect(getMine).not.toHaveBeenCalled()
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
    expect(getMine).not.toHaveBeenCalled()
  })

  it('redirects to "/" for a signed-out visitor', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: false, data: null })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/')
    expect(getMine).not.toHaveBeenCalled()
  })
})

describe('MyPostingsPage list', () => {
  beforeEach(() => {
    useSessionMock.mockReturnValue({
      isPending: false,
      isError: false,
      data: { kind: 'company', id: 'co-1', displayName: 'Cobalt Ledger' },
    })
  })

  it('shows skeleton rows while the query is pending', () => {
    getMine.mockReturnValue(new Promise(() => {}))
    renderPage()

    expect(getMine).toHaveBeenCalledWith(1, 20)
    const results = screen.getByRole('region', { name: 'My postings' })
    expect(results.querySelectorAll('[aria-hidden="true"]')).toHaveLength(3)
  })

  it('shows the error banner on a rejected query, and retry re-issues the identical request', async () => {
    const user = userEvent.setup()
    getMine.mockRejectedValueOnce({ status: 500, title: 'Server error' })
    renderPage()

    const banner = await screen.findByRole('alert')
    expect(banner).toHaveTextContent("We couldn't load your postings. Please try again.")
    expect(getMine).toHaveBeenCalledTimes(1)

    getMine.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 })
    await user.click(screen.getByRole('button', { name: 'Retry' }))

    await waitFor(() => expect(getMine).toHaveBeenCalledTimes(2))
    expect(getMine).toHaveBeenLastCalledWith(1, 20)
  })

  it('shows the empty-state copy and a link to Post a Job when there are no postings', async () => {
    getMine.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    renderPage()

    expect(await screen.findByText("You haven't posted a job yet.")).toBeInTheDocument()
    const link = screen.getByRole('link', { name: 'Post a Job' })
    expect(link).toHaveAttribute('href', '/post-a-job')
  })

  it('renders one row per posting, most-recent first as returned, linking to its applicants view', async () => {
    getMine.mockResolvedValue({
      items: [
        itemOf('jp-1', 'Staff Engineer', '2026-09-11T00:00:00Z'),
        itemOf('jp-2', 'Product Manager', '2026-09-10T00:00:00Z'),
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
      '/my-postings/jp-1/applicants',
    )
    expect(screen.getByRole('link', { name: /Product Manager/ })).toHaveAttribute(
      'href',
      '/my-postings/jp-2/applicants',
    )
    expect(screen.getByText('Page 1 of 1')).toBeInTheDocument()
  })

  it('clicking Next re-runs the query with page: 2 and renders the new page', async () => {
    const user = userEvent.setup()
    getMine
      .mockResolvedValueOnce({
        items: Array.from({ length: 20 }, (_, index) =>
          itemOf(`jp-${index + 1}`, `Title ${index + 1}`, '2026-09-11T00:00:00Z'),
        ),
        page: 1,
        pageSize: 20,
        total: 25,
      })
      .mockResolvedValueOnce({
        items: [itemOf('jp-21', 'Title 21', '2026-09-01T00:00:00Z')],
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
    expect(getMine).toHaveBeenLastCalledWith(2, 20)
  })
})
