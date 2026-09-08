import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { resetCsrfToken } from '../../../shared/api'

import { authClient } from './authClient'

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

const fetchMock = vi.fn()

function initOf(callIndex: number): RequestInit {
  return fetchMock.mock.calls[callIndex]?.[1] as RequestInit
}

function headerOf(callIndex: number, name: string): string | null {
  return new Headers(initOf(callIndex).headers).get(name)
}

function urlOf(callIndex: number): string {
  return String(fetchMock.mock.calls[callIndex]?.[0])
}

beforeEach(() => {
  resetCsrfToken()
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('authClient CSRF wiring', () => {
  it('seeds the token from GET /api/auth/csrf and sends it as X-CSRF-TOKEN on the POST', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(200, { id: 'c-1', accountType: 'company', displayName: 'Acme' }),
      )

    const result = await authClient.register({
      accountType: 'company',
      name: 'Acme',
      email: 'a@acme.test',
      password: 'password123',
    })

    expect(result.displayName).toBe('Acme')
    expect(urlOf(0)).toBe('/api/auth/csrf')
    expect(urlOf(1)).toBe('/api/auth/register')
    expect(headerOf(1, 'X-CSRF-TOKEN')).toBe('tok-1')
    expect(initOf(1).credentials).toBe('include')
  })

  it('clears the token, re-seeds, and retries once on an antiforgery 400', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(400, { title: 'Antiforgery token validation failed.', status: 400 }),
      )
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-2' }))
      .mockResolvedValueOnce(
        jsonResponse(200, { id: 'c-1', accountType: 'company', displayName: 'Acme' }),
      )

    const result = await authClient.login({
      accountType: 'company',
      email: 'a@acme.test',
      password: 'password123',
    })

    expect(result.displayName).toBe('Acme')
    expect(fetchMock).toHaveBeenCalledTimes(4)
    expect(headerOf(1, 'X-CSRF-TOKEN')).toBe('tok-1')
    expect(urlOf(2)).toBe('/api/auth/csrf')
    expect(headerOf(3, 'X-CSRF-TOKEN')).toBe('tok-2')
  })

  it('does not retry a 400 that carries a validation errors map — it rethrows', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(400, { title: 'One or more validation errors occurred.', status: 400, errors: { Email: ['Invalid.'] } }),
      )

    await expect(
      authClient.register({
        accountType: 'company',
        name: 'Acme',
        email: 'bad',
        password: 'password123',
      }),
    ).rejects.toMatchObject({ status: 400 })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('re-seeds only once — a second antiforgery 400 surfaces the failure', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(400, { title: 'Antiforgery token validation failed.', status: 400 }),
      )
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-2' }))
      .mockResolvedValueOnce(
        jsonResponse(400, { title: 'Antiforgery token validation failed.', status: 400 }),
      )

    await expect(authClient.logout()).rejects.toMatchObject({ status: 400 })
    expect(fetchMock).toHaveBeenCalledTimes(4)
  })

  it('sends no token and no csrf GET for me()', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, { id: 'c-1', accountType: 'company', displayName: 'Acme' }),
    )

    await authClient.me()

    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(urlOf(0)).toBe('/api/auth/me')
    expect(headerOf(0, 'X-CSRF-TOKEN')).toBeNull()
    expect(initOf(0).credentials).toBe('include')
  })
})
