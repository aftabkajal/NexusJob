import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { jobPostingsClient } from '../../entities/job-posting/api/jobPostingsClient'

import { HomePage } from './HomePage'

vi.mock('../../entities/job-posting/api/jobPostingsClient', () => ({
  jobPostingsClient: { search: vi.fn() },
}))

const search = vi.mocked(jobPostingsClient.search)

function renderHome() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(
    [
      { path: '/', element: <HomePage /> },
      { path: '/job-postings/:id', element: <div>detail-page</div> },
    ],
    { initialEntries: ['/'] },
  )
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

function resultOf(id: string, title: string, companyName: string, description: string) {
  return { id, title, companyName, description }
}

beforeEach(() => {
  search.mockReset()
})

describe('HomePage', () => {
  it('shows skeleton cards while the initial browse-all query is pending', () => {
    search.mockReturnValue(new Promise(() => {}))
    renderHome()

    expect(search).toHaveBeenCalledWith('', 1, 20)
    expect(screen.queryByText('No open postings yet. Check back soon.')).not.toBeInTheDocument()
    const results = screen.getByRole('region', { name: 'Open postings' })
    expect(results.querySelectorAll('[aria-hidden="true"]')).toHaveLength(3)
  })

  it('shows the empty-catalog copy when the browse-all search resolves with no postings', async () => {
    search.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    renderHome()

    expect(
      await screen.findByText('No open postings yet. Check back soon.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('navigation', { name: 'Search results pages' })).not.toBeInTheDocument()
  })

  it('renders the browse-all card stack and Pagination immediately on load, before any submit', async () => {
    search.mockResolvedValue({
      items: [
        resultOf('jp-1', 'Staff Engineer', 'Cobalt Ledger', 'Build the platform.'),
        resultOf('jp-2', 'Product Manager', 'Nimbus Works', 'Own the roadmap.'),
      ],
      page: 1,
      pageSize: 20,
      total: 2,
    })
    renderHome()

    expect(await screen.findByText('Staff Engineer')).toBeInTheDocument()
    expect(screen.getByText('Product Manager')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Staff Engineer/ })).toHaveAttribute(
      'href',
      '/job-postings/jp-1',
    )
    expect(screen.getByText('Page 1 of 1')).toBeInTheDocument()
    expect(search).toHaveBeenCalledWith('', 1, 20)
  })

  it('submitting a keyword that matches nothing shows the no-match copy with the keyword interpolated', async () => {
    const user = userEvent.setup()
    search
      .mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 })
      .mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 })
    renderHome()
    await screen.findByText('No open postings yet. Check back soon.')

    await user.type(screen.getByLabelText('Search open postings'), 'astrophysics')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    expect(
      await screen.findByText('No postings match "astrophysics." Try a different term.'),
    ).toBeInTheDocument()
    expect(search).toHaveBeenCalledWith('astrophysics', 1, 20)
  })

  it('trims the submitted keyword and resets to page 1 on submit', async () => {
    const user = userEvent.setup()
    search.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    renderHome()
    await screen.findByText('No open postings yet. Check back soon.')

    await user.type(screen.getByLabelText('Search open postings'), '  engineer  ')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() => expect(search).toHaveBeenLastCalledWith('engineer', 1, 20))
  })

  it('typing without submitting never calls search again', async () => {
    const user = userEvent.setup()
    search.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })
    renderHome()
    await screen.findByText('No open postings yet. Check back soon.')

    search.mockClear()
    await user.type(screen.getByLabelText('Search open postings'), 'engineer')

    expect(search).not.toHaveBeenCalled()
  })

  it('clicking Next re-runs the query with page: 2 and renders the new page', async () => {
    const user = userEvent.setup()
    search
      .mockResolvedValueOnce({
        items: Array.from({ length: 20 }, (_, index) =>
          resultOf(`jp-${index + 1}`, `Title ${index + 1}`, 'Cobalt Ledger', 'Description.'),
        ),
        page: 1,
        pageSize: 20,
        total: 25,
      })
      .mockResolvedValueOnce({
        items: [resultOf('jp-21', 'Title 21', 'Cobalt Ledger', 'Description.')],
        page: 2,
        pageSize: 20,
        total: 25,
      })
    renderHome()

    await screen.findByText('Page 1 of 2')
    expect(screen.getByRole('button', { name: 'Prev' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()

    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(await screen.findByText('Page 2 of 2')).toBeInTheDocument()
    expect(screen.getByText('Title 21')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(search).toHaveBeenLastCalledWith('', 2, 20)
  })

  it('submitting a new keyword while on page 2 re-runs the search at page: 1', async () => {
    const user = userEvent.setup()
    search
      .mockResolvedValueOnce({
        items: Array.from({ length: 20 }, (_, index) =>
          resultOf(`jp-${index + 1}`, `Title ${index + 1}`, 'Cobalt Ledger', 'Description.'),
        ),
        page: 1,
        pageSize: 20,
        total: 25,
      })
      .mockResolvedValueOnce({
        items: [resultOf('jp-21', 'Title 21', 'Cobalt Ledger', 'Description.')],
        page: 2,
        pageSize: 20,
        total: 25,
      })
      .mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 })
    renderHome()

    await screen.findByText('Page 1 of 2')
    await user.click(screen.getByRole('button', { name: 'Next' }))
    await screen.findByText('Page 2 of 2')

    await user.type(screen.getByLabelText('Search open postings'), 'astrophysics')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() => expect(search).toHaveBeenLastCalledWith('astrophysics', 1, 20))
  })

  it('shows the error banner on a rejected query, and retry re-issues the identical request', async () => {
    const user = userEvent.setup()
    search.mockRejectedValueOnce({ status: 500, title: 'Server error' })
    renderHome()

    const banner = await screen.findByRole('alert')
    expect(banner).toBeInTheDocument()
    expect(search).toHaveBeenCalledTimes(1)
    expect(search).toHaveBeenCalledWith('', 1, 20)

    search.mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 0 })
    await user.click(screen.getByRole('button', { name: 'Retry' }))

    await waitFor(() => expect(search).toHaveBeenCalledTimes(2))
    expect(search).toHaveBeenLastCalledWith('', 1, 20)
  })
})
