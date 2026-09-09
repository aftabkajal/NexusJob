import type { ReactNode } from 'react'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { toApiError } from '../../../shared/lib'
import { jobPostingsClient } from '../api/jobPostingsClient'

import {
  jobPostingQueryKey,
  jobPostingSearchQueryKey,
  useJobPosting,
  useJobPostingSearch,
} from './jobPostingQuery'

vi.mock('../api/jobPostingsClient', () => ({
  jobPostingsClient: { getById: vi.fn(), search: vi.fn() },
}))

const getById = vi.mocked(jobPostingsClient.getById)
const search = vi.mocked(jobPostingsClient.search)

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

beforeEach(() => {
  getById.mockReset()
  search.mockReset()
})

describe('jobPostingQueryKey', () => {
  it('is owned by the slice and scoped to the id', () => {
    expect(jobPostingQueryKey('jp-1')).toEqual(['job-posting', 'jp-1'])
  })
})

describe('useJobPosting', () => {
  it('resolves the posting detail into isSuccess + data', async () => {
    getById.mockResolvedValue({
      id: 'jp-1',
      title: 'Staff Engineer',
      description: 'Build the platform.',
      companyName: 'Cobalt Ledger',
    })

    const { result } = renderHook(() => useJobPosting('jp-1'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual({
      id: 'jp-1',
      title: 'Staff Engineer',
      description: 'Build the platform.',
      companyName: 'Cobalt Ledger',
    })
    expect(getById).toHaveBeenCalledWith('jp-1')
  })

  it('surfaces a 404 as a query error the page can branch on via toApiError', async () => {
    getById.mockRejectedValue({ status: 404, title: 'Not Found' })

    const { result } = renderHook(() => useJobPosting('missing'), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(toApiError(result.current.error)?.status).toBe(404)
  })
})

describe('jobPostingSearchQueryKey', () => {
  it('is owned by the slice and scoped to the query, page, and pageSize', () => {
    expect(jobPostingSearchQueryKey('engineer', 2, 20)).toEqual([
      'job-postings',
      'search',
      'engineer',
      2,
      20,
    ])
  })
})

describe('useJobPostingSearch', () => {
  it('resolves the search page into isSuccess + data', async () => {
    search.mockResolvedValue({
      items: [
        {
          id: 'jp-1',
          title: 'Staff Engineer',
          description: 'Build the platform.',
          companyName: 'Cobalt Ledger',
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })

    const { result } = renderHook(() => useJobPostingSearch('engineer', 1, 20), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.total).toBe(1)
    expect(search).toHaveBeenCalledWith('engineer', 1, 20)
  })

  it('runs unconditionally with an empty query (browse-all)', async () => {
    search.mockResolvedValue({ items: [], page: 1, pageSize: 20, total: 0 })

    const { result } = renderHook(() => useJobPostingSearch('', 1, 20), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(search).toHaveBeenCalledWith('', 1, 20)
  })

  it('surfaces a rejection as a query error', async () => {
    search.mockRejectedValue({ status: 500, title: 'Server error' })

    const { result } = renderHook(() => useJobPostingSearch('engineer', 1, 20), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(toApiError(result.current.error)?.status).toBe(500)
  })
})
