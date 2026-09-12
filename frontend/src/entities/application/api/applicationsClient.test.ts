import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { resetCsrfToken } from '../../../shared/api'

import { applicationsClient } from './applicationsClient'

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

describe('applicationsClient.apply', () => {
  it('POSTs /api/applications with credentials and a seeded X-CSRF-TOKEN, resolving the ApplicationResponse', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(200, {
          id: 'app-1',
          jobPostingId: 'jp-1',
          submittedAt: '2026-09-11T00:00:00Z',
        }),
      )

    const result = await applicationsClient.apply({ jobPostingId: 'jp-1' })

    expect(result.id).toBe('app-1')
    expect(urlOf(0)).toBe('/api/auth/csrf')
    expect(urlOf(1)).toBe('/api/applications')
    expect(initOf(1).method).toBe('POST')
    expect(headerOf(1, 'X-CSRF-TOKEN')).toBe('tok-1')
    expect(initOf(1).credentials).toBe('include')
  })

  it('re-seeds the token and retries once on an antiforgery 400', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(400, { title: 'Antiforgery token validation failed.', status: 400 }),
      )
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-2' }))
      .mockResolvedValueOnce(
        jsonResponse(200, {
          id: 'app-2',
          jobPostingId: 'jp-2',
          submittedAt: '2026-09-11T00:00:00Z',
        }),
      )

    const result = await applicationsClient.apply({ jobPostingId: 'jp-2' })

    expect(result.id).toBe('app-2')
    expect(fetchMock).toHaveBeenCalledTimes(4)
    expect(headerOf(1, 'X-CSRF-TOKEN')).toBe('tok-1')
    expect(urlOf(2)).toBe('/api/auth/csrf')
    expect(headerOf(3, 'X-CSRF-TOKEN')).toBe('tok-2')
  })

  it('returns 200 with the existing row on a repeat apply (3.1a is idempotent)', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(200, {
          id: 'app-existing',
          jobPostingId: 'jp-1',
          submittedAt: '2026-09-10T00:00:00Z',
        }),
      )

    const result = await applicationsClient.apply({ jobPostingId: 'jp-1' })

    expect(result.id).toBe('app-existing')
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('rethrows a 401 / 403 for a caller that is not a Job Seeker, without retrying', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(jsonResponse(403, { title: 'Forbidden', status: 403 }))

    await expect(applicationsClient.apply({ jobPostingId: 'jp-1' })).rejects.toMatchObject({
      status: 403,
    })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('rethrows a 404 for a missing posting, without retrying', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(jsonResponse(404, { title: 'Not Found', status: 404 }))

    await expect(applicationsClient.apply({ jobPostingId: 'missing' })).rejects.toMatchObject({
      status: 404,
    })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})

describe('applicationsClient.getMine', () => {
  it('issues GET /api/applications/mine with credentials, no CSRF, resolving the MyApplicationResponse', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, { applied: true, appliedAt: '2026-09-10T00:00:00Z' }),
    )

    const result = await applicationsClient.getMine('jp-1')

    expect(result).toEqual({ applied: true, appliedAt: '2026-09-10T00:00:00Z' })
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(urlOf(0)).toBe('/api/applications/mine?jobPostingId=jp-1')
    expect((initOf(0).method ?? 'GET').toUpperCase()).toBe('GET')
    expect(initOf(0).credentials).toBe('include')
    expect(headerOf(0, 'X-CSRF-TOKEN')).toBeNull()
  })

  it('resolves { applied: false } when the Job Seeker has not applied', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { applied: false, appliedAt: null }))

    const result = await applicationsClient.getMine('jp-1')

    expect(result.applied).toBe(false)
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('propagates a 401 / 403 unchanged, with no CSRF seed or retry', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(401, { title: 'Unauthorized', status: 401 }))

    await expect(applicationsClient.getMine('jp-1')).rejects.toMatchObject({ status: 401 })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})

describe('applicationsClient.getMyApplications', () => {
  it('issues GET /api/applications/mine/list with credentials, no CSRF, resolving the page', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, {
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
      }),
    )

    const result = await applicationsClient.getMyApplications(1, 20)

    expect(result.total).toBe(1)
    expect(result.items[0]?.applicationId).toBe('app-1')
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(urlOf(0)).toBe('/api/applications/mine/list?page=1&pageSize=20')
    expect((initOf(0).method ?? 'GET').toUpperCase()).toBe('GET')
    expect(initOf(0).credentials).toBe('include')
    expect(headerOf(0, 'X-CSRF-TOKEN')).toBeNull()
  })

  it('propagates a rejection unchanged, with no CSRF seed or retry', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(401, { title: 'Unauthorized', status: 401 }))

    await expect(applicationsClient.getMyApplications(1, 20)).rejects.toMatchObject({
      status: 401,
    })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })
})
