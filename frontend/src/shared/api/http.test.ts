import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import {
  callWithCsrfRetry,
  ensureCsrfToken,
  peekCsrfToken,
  resetCsrfToken,
  withCredentialsAndCsrf,
} from './http'

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const fetchMock = vi.fn()

beforeEach(() => {
  resetCsrfToken()
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('shared/api/http — CSRF token cache', () => {
  it('ensureCsrfToken fetches GET /api/auth/csrf once and caches the token', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, { token: 'tok-1' }))

    expect(peekCsrfToken()).toBeNull()
    await ensureCsrfToken()
    await ensureCsrfToken()

    expect(peekCsrfToken()).toBe('tok-1')
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(String(fetchMock.mock.calls[0]?.[0])).toBe('/api/auth/csrf')
  })

  it('resetCsrfToken clears the cache so the next ensureCsrfToken re-seeds', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-2' }))

    await ensureCsrfToken()
    resetCsrfToken()
    expect(peekCsrfToken()).toBeNull()
    await ensureCsrfToken()

    expect(peekCsrfToken()).toBe('tok-2')
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})

describe('shared/api/http — withCredentialsAndCsrf', () => {
  it('always sets credentials: include', () => {
    expect(withCredentialsAndCsrf({ method: 'GET' }).credentials).toBe('include')
    expect(withCredentialsAndCsrf(undefined).credentials).toBe('include')
  })

  it('adds X-CSRF-TOKEN from the cache on POST only', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, { token: 'tok-1' }))
    await ensureCsrfToken()

    const post = withCredentialsAndCsrf({ method: 'POST' })
    expect(new Headers(post.headers).get('X-CSRF-TOKEN')).toBe('tok-1')

    const get = withCredentialsAndCsrf({ method: 'GET' })
    expect(new Headers(get.headers).get('X-CSRF-TOKEN')).toBeNull()
  })
})

describe('shared/api/http — callWithCsrfRetry', () => {
  it('seeds the token before running the call', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
    const call = vi.fn().mockResolvedValue('ok')

    const result = await callWithCsrfRetry(call)

    expect(result).toBe('ok')
    expect(peekCsrfToken()).toBe('tok-1')
    expect(call).toHaveBeenCalledTimes(1)
  })

  it('clears the token, re-seeds, and retries once on an antiforgery 400', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-2' }))
    const call = vi
      .fn()
      .mockRejectedValueOnce({ status: 400, title: 'Antiforgery token validation failed.' })
      .mockResolvedValueOnce('ok')

    const result = await callWithCsrfRetry(call)

    expect(result).toBe('ok')
    expect(call).toHaveBeenCalledTimes(2)
    expect(peekCsrfToken()).toBe('tok-2')
  })

  it('rethrows a 400 that carries a validation errors map — no retry', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
    const call = vi
      .fn()
      .mockRejectedValue({ status: 400, title: 'validation', errors: { Title: ['Required.'] } })

    await expect(callWithCsrfRetry(call)).rejects.toMatchObject({ status: 400 })
    expect(call).toHaveBeenCalledTimes(1)
  })

  it('re-seeds only once — a second antiforgery 400 surfaces the failure', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-2' }))
    const call = vi
      .fn()
      .mockRejectedValue({ status: 400, title: 'Antiforgery token validation failed.' })

    await expect(callWithCsrfRetry(call)).rejects.toMatchObject({ status: 400 })
    expect(call).toHaveBeenCalledTimes(2)
  })
})
