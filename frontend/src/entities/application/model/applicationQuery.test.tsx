import type { ReactNode } from 'react'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { toApiError } from '../../../shared/lib'
import { applicationsClient } from '../api/applicationsClient'

import { applicationMineQueryKey, useMyApplication } from './applicationQuery'

vi.mock('../api/applicationsClient', () => ({
  applicationsClient: { apply: vi.fn(), getMine: vi.fn() },
}))

const getMine = vi.mocked(applicationsClient.getMine)

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

beforeEach(() => {
  getMine.mockReset()
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
