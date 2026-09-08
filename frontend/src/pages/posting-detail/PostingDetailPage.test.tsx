import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { jobPostingsClient } from '../../entities/job-posting/api/jobPostingsClient'

import { PostingDetailPage } from './PostingDetailPage'

vi.mock('../../entities/job-posting/api/jobPostingsClient', () => ({
  jobPostingsClient: { getById: vi.fn() },
}))

const getById = vi.mocked(jobPostingsClient.getById)

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
