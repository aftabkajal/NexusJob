import type { ReactNode } from 'react'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { toApiError } from '../../../shared/lib'
import { applicationsClient } from '../api/applicationsClient'

import {
  applicantsQueryKey,
  applicationMineQueryKey,
  applicationsMineListQueryKey,
  useApplicants,
  useMyApplication,
  useMyApplications,
} from './applicationQuery'

vi.mock('../api/applicationsClient', () => ({
  applicationsClient: {
    apply: vi.fn(),
    getMine: vi.fn(),
    getMyApplications: vi.fn(),
    getApplicants: vi.fn(),
  },
}))

const getMine = vi.mocked(applicationsClient.getMine)
const getMyApplications = vi.mocked(applicationsClient.getMyApplications)
const getApplicants = vi.mocked(applicationsClient.getApplicants)

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

beforeEach(() => {
  getMine.mockReset()
  getMyApplications.mockReset()
  getApplicants.mockReset()
})

describe('applicationMineQueryKey', () => {
  it('is owned by the slice and scoped to the posting id', () => {
    expect(applicationMineQueryKey('jp-1')).toEqual(['application', 'mine', 'jp-1'])
  })
})

describe('useMyApplication', () => {
  it('resolves the applied-state into isSuccess + data when enabled', async () => {
    getMine.mockResolvedValue({ applied: true, appliedAt: '2026-09-10T00:00:00Z' })

    const { result } = renderHook(() => useMyApplication('jp-1', true), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual({ applied: true, appliedAt: '2026-09-10T00:00:00Z' })
    expect(getMine).toHaveBeenCalledWith('jp-1')
  })

  it('does not fire the query when enabled is false (401/403 endpoint)', async () => {
    getMine.mockResolvedValue({ applied: false, appliedAt: undefined })

    const { result } = renderHook(() => useMyApplication('jp-1', false), { wrapper })

    // Give any (incorrect) fire a chance to happen.
    await Promise.resolve()
    expect(getMine).not.toHaveBeenCalled()
    expect(result.current.fetchStatus).toBe('idle')
  })

  it('surfaces a rejection as a query error the caller can branch on via toApiError', async () => {
    getMine.mockRejectedValue({ status: 401, title: 'Unauthorized' })

    const { result } = renderHook(() => useMyApplication('jp-1', true), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(toApiError(result.current.error)?.status).toBe(401)
  })
})

describe('applicationsMineListQueryKey', () => {
  it('is owned by the slice and scoped to page and pageSize', () => {
    expect(applicationsMineListQueryKey(1, 20)).toEqual(['application', 'mine', 'list', 1, 20])
  })
})

describe('useMyApplications', () => {
  it('resolves the page into isSuccess + data', async () => {
    getMyApplications.mockResolvedValue({
      items: [
        {
          applicationId: 'app-1',
          jobPostingId: 'jp-1',
          jobPostingTitle: 'Staff Engineer',
          submittedAt: '2026-09-11T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })

    const { result } = renderHook(() => useMyApplications(1, 20), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.total).toBe(1)
    expect(getMyApplications).toHaveBeenCalledWith(1, 20)
  })

  it('surfaces a rejection as a query error the caller can branch on via toApiError', async () => {
    getMyApplications.mockRejectedValue({ status: 500, title: 'Server error' })

    const { result } = renderHook(() => useMyApplications(1, 20), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(toApiError(result.current.error)?.status).toBe(500)
  })
})

describe('applicantsQueryKey', () => {
  it('is owned by the slice and scoped to the posting id, page, and pageSize', () => {
    expect(applicantsQueryKey('jp-1', 1, 20)).toEqual(['application', 'applicants', 'jp-1', 1, 20])
  })
})

describe('useApplicants', () => {
  it('resolves the page into isSuccess + data', async () => {
    getApplicants.mockResolvedValue({
      items: [
        {
          jobSeekerId: 'js-1',
          fullName: 'Priya Raman',
          email: 'priya@example.com',
          submittedAt: '2026-09-11T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })

    const { result } = renderHook(() => useApplicants('jp-1', 1, 20), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.total).toBe(1)
    expect(getApplicants).toHaveBeenCalledWith('jp-1', 1, 20)
  })

  it('surfaces a 404 as a query error the page can branch on via toApiError', async () => {
    getApplicants.mockRejectedValue({ status: 404, title: 'Not Found' })

    const { result } = renderHook(() => useApplicants('missing', 1, 20), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(toApiError(result.current.error)?.status).toBe(404)
  })
})
