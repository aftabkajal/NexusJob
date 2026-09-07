import type { ReactNode } from 'react'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { authClient } from '../api/authClient'

import { useSession } from './sessionQuery'

vi.mock('../api/authClient', () => ({
  authClient: { me: vi.fn() },
}))

const me = vi.mocked(authClient.me)

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

beforeEach(() => {
  me.mockReset()
})

describe('useSession', () => {
  it('maps a resolved account to a Company viewer', async () => {
    me.mockResolvedValue({ id: 'c-1', accountType: 'company', displayName: 'Cobalt Ledger' })

    const { result } = renderHook(() => useSession(), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual({
      kind: 'company',
      id: 'c-1',
      displayName: 'Cobalt Ledger',
    })
  })

  it('maps a resolved job_seeker account to a Job Seeker viewer', async () => {
    me.mockResolvedValue({ id: 'js-1', accountType: 'job_seeker', displayName: 'Priya Raman' })

    const { result } = renderHook(() => useSession(), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual({
      kind: 'jobSeeker',
      id: 'js-1',
      displayName: 'Priya Raman',
    })
  })

  it('puts the query in isError when GET /api/auth/me resolves an unknown accountType', async () => {
    me.mockResolvedValue({ id: 'x-1', accountType: 'admin', displayName: 'Root' })

    const { result } = renderHook(() => useSession(), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.data).toBeUndefined()
  })

  it('treats a 401 from GET /api/auth/me as "no viewer" (null), not an error', async () => {
    me.mockRejectedValue({ status: 401, title: 'Unauthorized' })

    const { result } = renderHook(() => useSession(), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toBeNull()
    expect(result.current.isError).toBe(false)
  })

  it('propagates a non-401 failure as a query error', async () => {
    me.mockRejectedValue({ status: 500, title: 'Server error' })

    const { result } = renderHook(() => useSession(), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
  })
})
