import type { ReactNode } from 'react'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { useApplyToPosting } from './useApplyToPosting'

const useSessionMock = vi.fn()
const useMyApplicationMock = vi.fn()
const applyMock = vi.fn()

vi.mock('../../../entities', () => ({
  useSession: () => useSessionMock(),
  useMyApplication: (jobPostingId: string, enabled: boolean) =>
    useMyApplicationMock(jobPostingId, enabled),
  applicationsClient: { apply: (body: unknown) => applyMock(body) },
  applicationMineQueryKey: (jobPostingId: string) => ['application', 'mine', jobPostingId],
}))

const APPLY_FAILED_MESSAGE = "We couldn't submit your application. Please try again."

const jobSeekerSession = {
  isPending: false,
  isError: false,
  data: { kind: 'jobSeeker', id: 'js-1', displayName: 'Amara' },
}
const companySession = {
  isPending: false,
  isError: false,
  data: { kind: 'company', id: 'co-1', displayName: 'Cobalt Ledger' },
}

const mineNotApplied = { isPending: false, isError: false, data: { applied: false, appliedAt: undefined } }
const mineApplied = {
  isPending: false,
  isError: false,
  data: { applied: true, appliedAt: '2026-09-10T00:00:00Z' },
}
const minePending = { isPending: true, isError: false, data: undefined }
const mineErrored = { isPending: false, isError: true, data: undefined }

function setup() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  return { ...renderHook(() => useApplyToPosting('jp-1'), { wrapper }), invalidateSpy }
}

beforeEach(() => {
  useSessionMock.mockReturnValue(jobSeekerSession)
  useMyApplicationMock.mockReturnValue(mineNotApplied)
  applyMock.mockReset()
})

describe('useApplyToPosting — render gate', () => {
  it('renders the button for a signed-in Job Seeker who has not applied', () => {
    const { result } = setup()

    expect(result.current.render).toBe('button')
    expect(result.current.applied).toBe(false)
    expect(result.current.disabled).toBe(false)
    expect(result.current.showConfirmation).toBe(false)
  })

  it('is hidden for a signed-in Company and does not enable the mine query', () => {
    useSessionMock.mockReturnValue(companySession)
    const { result } = setup()

    expect(result.current.render).toBe('hidden')
    expect(useMyApplicationMock).toHaveBeenCalledWith('jp-1', false)
  })

  it('is hidden while the session is still pending', () => {
    useSessionMock.mockReturnValue({ isPending: true, isError: false, data: undefined })
    expect(setup().result.current.render).toBe('hidden')
  })

  it('is hidden when the session query has errored', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: true, data: undefined })
    expect(setup().result.current.render).toBe('hidden')
  })

  it('renders the apply-gate for a signed-out visitor and does not enable the mine query', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: false, data: null })
    const { result } = setup()

    expect(result.current.render).toBe('gate')
    expect(useMyApplicationMock).toHaveBeenCalledWith('jp-1', false)
  })
})

describe('useApplyToPosting — mine query state', () => {
  it('renders a disabled Apply while the mine query is pending (no flash)', () => {
    useMyApplicationMock.mockReturnValue(minePending)
    const { result } = setup()

    expect(result.current.render).toBe('button')
    expect(result.current.applied).toBe(false)
    expect(result.current.disabled).toBe(true)
  })

  it('renders an enabled Apply when the mine query errors', () => {
    useMyApplicationMock.mockReturnValue(mineErrored)
    const { result } = setup()

    expect(result.current.applied).toBe(false)
    expect(result.current.disabled).toBe(false)
  })

  it('renders Applied on load when the Job Seeker has already applied, with no confirmation', () => {
    useMyApplicationMock.mockReturnValue(mineApplied)
    const { result } = setup()

    expect(result.current.applied).toBe(true)
    expect(result.current.disabled).toBe(true)
    expect(result.current.showConfirmation).toBe(false)
  })
})

describe('useApplyToPosting — inline submit', () => {
  it('sends exactly one apply, flips to the confirmation state, and invalidates the mine key on success', async () => {
    applyMock.mockResolvedValue({ id: 'app-1', jobPostingId: 'jp-1', submittedAt: '2026-09-11T00:00:00Z' })
    const { result, invalidateSpy } = setup()

    await act(async () => {
      result.current.apply()
    })

    await waitFor(() => expect(result.current.showConfirmation).toBe(true))
    expect(applyMock).toHaveBeenCalledTimes(1)
    expect(applyMock).toHaveBeenCalledWith({ jobPostingId: 'jp-1' })
    expect(result.current.applied).toBe(true)
    expect(result.current.disabled).toBe(true)
    expect(result.current.formError).toBeUndefined()
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['application', 'mine', 'jp-1'] })
    expect(invalidateSpy).toHaveBeenCalledTimes(1)
  })

  it('does not fire a second apply while the first request is in flight', async () => {
    applyMock.mockImplementation(() => new Promise(() => {}))
    const { result } = setup()

    await act(async () => {
      result.current.apply()
    })
    await waitFor(() => expect(result.current.isSubmitting).toBe(true))
    await act(async () => {
      result.current.apply()
    })

    expect(applyMock).toHaveBeenCalledTimes(1)
    expect(result.current.isSubmitting).toBe(true)
    expect(result.current.disabled).toBe(true)
  })

  it('shows the inline failure message and re-enables Apply on error, then a retry re-submits to success', async () => {
    applyMock
      .mockRejectedValueOnce({ status: 500, title: 'Server error' })
      .mockResolvedValueOnce({ id: 'app-2', jobPostingId: 'jp-1', submittedAt: '2026-09-11T00:00:00Z' })
    const { result } = setup()

    await act(async () => {
      result.current.apply()
    })

    await waitFor(() => expect(result.current.formError).toBe(APPLY_FAILED_MESSAGE))
    expect(result.current.applied).toBe(false)
    expect(result.current.disabled).toBe(false)
    expect(result.current.showConfirmation).toBe(false)

    await act(async () => {
      result.current.apply()
    })

    await waitFor(() => expect(result.current.showConfirmation).toBe(true))
    expect(applyMock).toHaveBeenCalledTimes(2)
    expect(result.current.formError).toBeUndefined()
    expect(result.current.applied).toBe(true)
  })

  it('clears the stale failure message as soon as a retry begins, before it resolves', async () => {
    applyMock.mockRejectedValueOnce({ status: 500, title: 'Server error' })
    const { result } = setup()

    await act(async () => {
      result.current.apply()
    })
    await waitFor(() => expect(result.current.formError).toBe(APPLY_FAILED_MESSAGE))

    let resolveRetry: (value: unknown) => void = () => {}
    applyMock.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveRetry = resolve
        }),
    )

    await act(async () => {
      result.current.apply()
    })
    await waitFor(() => expect(result.current.isSubmitting).toBe(true))

    // Cleared at the start of the retry — no stale `role="alert"` banner
    // survives into the in-flight window.
    expect(result.current.formError).toBeUndefined()

    await act(async () => {
      resolveRetry({ id: 'app-3', jobPostingId: 'jp-1', submittedAt: '2026-09-11T00:00:00Z' })
    })
    await waitFor(() => expect(result.current.showConfirmation).toBe(true))
  })
})
