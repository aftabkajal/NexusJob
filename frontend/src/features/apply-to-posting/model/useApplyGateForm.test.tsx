import type { FormEvent, ReactNode } from 'react'

import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { authClient } from '../../../entities'

import { useApplyGateForm } from './useApplyGateForm'

/** `handleSubmit` only ever reads `.preventDefault()` off its event arg. */
const fakeSubmitEvent = { preventDefault: () => {} } as FormEvent<HTMLFormElement>

vi.mock('../../../entities', () => ({
  authClient: { register: vi.fn() },
}))

const register = vi.mocked(authClient.register)

const DUPLICATE_EMAIL_MESSAGE = 'This email is already registered as a Job Seeker.'

function setup(onAuthenticated: () => void) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  return renderHook(() => useApplyGateForm(onAuthenticated), { wrapper })
}

beforeEach(() => {
  register.mockReset()
})

describe('useApplyGateForm', () => {
  it('registers a Job Seeker and calls onAuthenticated exactly once on success', async () => {
    register.mockResolvedValue({ id: 'js-1', accountType: 'job_seeker', displayName: 'Amara Diallo' })
    const onAuthenticated = vi.fn()
    const { result } = setup(onAuthenticated)

    act(() => {
      result.current.setField('name', 'Amara Diallo')
      result.current.setField('email', 'amara@seeker.test')
      result.current.setField('password', 'password123')
    })

    await act(async () => {
      result.current.handleSubmit(fakeSubmitEvent)
    })

    await waitFor(() => expect(onAuthenticated).toHaveBeenCalledTimes(1))
    expect(register).toHaveBeenCalledWith({
      accountType: 'job_seeker',
      name: 'Amara Diallo',
      email: 'amara@seeker.test',
      password: 'password123',
    })
  })

  it('shows the duplicate-email field error on a 409 and never calls onAuthenticated', async () => {
    register.mockRejectedValue({ status: 409, title: 'Conflict' })
    const onAuthenticated = vi.fn()
    const { result } = setup(onAuthenticated)

    act(() => {
      result.current.setField('name', 'Amara Diallo')
      result.current.setField('email', 'taken@seeker.test')
      result.current.setField('password', 'password123')
    })

    await act(async () => {
      result.current.handleSubmit(fakeSubmitEvent)
    })

    await waitFor(() => expect(result.current.fieldErrors.email).toBe(DUPLICATE_EMAIL_MESSAGE))
    expect(onAuthenticated).not.toHaveBeenCalled()
  })

  it('maps a server 400 errors entry onto the matching field', async () => {
    register.mockRejectedValue({ status: 400, errors: { Email: ['That email address is not permitted.'] } })
    const { result } = setup(vi.fn())

    act(() => {
      result.current.setField('name', 'Amara Diallo')
      result.current.setField('email', 'blocked@seeker.test')
      result.current.setField('password', 'password123')
    })

    await act(async () => {
      result.current.handleSubmit(fakeSubmitEvent)
    })

    await waitFor(() =>
      expect(result.current.fieldErrors.email).toBe('That email address is not permitted.'),
    )
  })

  it('falls back to the generic message for any other failure', async () => {
    register.mockRejectedValue({ status: 500, title: 'Server error' })
    const { result } = setup(vi.fn())

    act(() => {
      result.current.setField('name', 'Amara Diallo')
      result.current.setField('email', 'amara@seeker.test')
      result.current.setField('password', 'password123')
    })

    await act(async () => {
      result.current.handleSubmit(fakeSubmitEvent)
    })

    await waitFor(() =>
      expect(result.current.formError).toBe('We could not complete your request. Please try again.'),
    )
  })

  it('blocks submit on blank fields with client-side validation and sends no request', () => {
    const { result } = setup(vi.fn())

    act(() => {
      result.current.handleSubmit(fakeSubmitEvent)
    })

    expect(result.current.fieldErrors.name).toBe('Enter your full name.')
    expect(result.current.fieldErrors.email).toBe('Enter your email address.')
    expect(result.current.fieldErrors.password).toBe('Enter your password.')
    expect(register).not.toHaveBeenCalled()
  })

  it('validates a field on blur', () => {
    const { result } = setup(vi.fn())

    act(() => {
      result.current.blurField('email')
    })

    expect(result.current.fieldErrors.email).toBe('Enter your email address.')
  })

  it('is fixed to the jobSeeker role', () => {
    const { result } = setup(vi.fn())

    expect(result.current.role).toBe('jobSeeker')
  })

  it('does not fire a second register while the first request is in flight', async () => {
    register.mockImplementation(() => new Promise(() => {}))
    const onAuthenticated = vi.fn()
    const { result } = setup(onAuthenticated)

    act(() => {
      result.current.setField('name', 'Amara Diallo')
      result.current.setField('email', 'amara@seeker.test')
      result.current.setField('password', 'password123')
    })

    await act(async () => {
      result.current.handleSubmit(fakeSubmitEvent)
    })
    await waitFor(() => expect(result.current.isSubmitting).toBe(true))

    await act(async () => {
      result.current.handleSubmit(fakeSubmitEvent)
    })

    expect(register).toHaveBeenCalledTimes(1)
  })
})
