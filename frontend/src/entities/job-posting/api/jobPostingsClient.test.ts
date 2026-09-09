import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { resetCsrfToken } from '../../../shared/api'

import { jobPostingsClient } from './jobPostingsClient'

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

describe('jobPostingsClient.create', () => {
  it('POSTs /api/job-postings with credentials and a seeded X-CSRF-TOKEN, resolving the JobPostingResponse', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(200, {
          id: 'jp-1',
          title: 'Staff Engineer',
          description: 'Build the platform.',
          createdAt: '2026-09-08T00:00:00Z',
        }),
      )

    const result = await jobPostingsClient.create({
      title: 'Staff Engineer',
      description: 'Build the platform.',
    })

    expect(result.id).toBe('jp-1')
    expect(urlOf(0)).toBe('/api/auth/csrf')
    expect(urlOf(1)).toBe('/api/job-postings')
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
          id: 'jp-2',
          title: 'Product Manager',
          description: 'Own the roadmap.',
          createdAt: '2026-09-08T00:00:00Z',
        }),
      )

    const result = await jobPostingsClient.create({
      title: 'Product Manager',
      description: 'Own the roadmap.',
    })

    expect(result.id).toBe('jp-2')
    expect(fetchMock).toHaveBeenCalledTimes(4)
    expect(headerOf(1, 'X-CSRF-TOKEN')).toBe('tok-1')
    expect(urlOf(2)).toBe('/api/auth/csrf')
    expect(headerOf(3, 'X-CSRF-TOKEN')).toBe('tok-2')
  })

  it('rethrows a validation 400 (errors map) without retrying', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(
        jsonResponse(400, {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { Title: ['The Title field is required.'] },
        }),
      )

    await expect(
      jobPostingsClient.create({ title: '', description: 'x' }),
    ).rejects.toMatchObject({ status: 400 })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('rethrows a 401 / 403 for a non-Company caller (defence in depth)', async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, { token: 'tok-1' }))
      .mockResolvedValueOnce(jsonResponse(403, { title: 'Forbidden', status: 403 }))

    await expect(
      jobPostingsClient.create({ title: 'x', description: 'y' }),
    ).rejects.toMatchObject({ status: 403 })
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})

describe('jobPostingsClient.getById', () => {
  it('issues GET /api/job-postings/{id} with credentials and resolves the JobPostingDetailResponse', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, {
        id: 'jp-1',
        title: 'Staff Engineer',
        description: 'Build the platform.',
        companyName: 'Cobalt Ledger',
      }),
    )

    const result = await jobPostingsClient.getById('jp-1')

    expect(result).toEqual({
      id: 'jp-1',
      title: 'Staff Engineer',
      description: 'Build the platform.',
      companyName: 'Cobalt Ledger',
    })
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(urlOf(0)).toBe('/api/job-postings/jp-1')
    expect((initOf(0).method ?? 'GET').toUpperCase()).toBe('GET')
    expect(initOf(0).credentials).toBe('include')
    expect(headerOf(0, 'X-CSRF-TOKEN')).toBeNull()
  })

  it('propagates a 404 unchanged, with no CSRF seed or retry', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(404, {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
        title: 'Not Found',
        status: 404,
      }),
    )

    await expect(jobPostingsClient.getById('missing')).rejects.toMatchObject({ status: 404 })
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(urlOf(0)).toBe('/api/job-postings/missing')
  })
})

describe('jobPostingsClient.search', () => {
  it('issues GET /api/job-postings with the three params and resolves the PageOfJobPostingSearchResultResponse', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, {
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
      }),
    )

    const result = await jobPostingsClient.search('engineer', 1, 20)

    expect(result).toEqual({
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
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(urlOf(0)).toBe('/api/job-postings?query=engineer&page=1&pageSize=20')
    expect((initOf(0).method ?? 'GET').toUpperCase()).toBe('GET')
    expect(headerOf(0, 'X-CSRF-TOKEN')).toBeNull()
  })

  it('runs a browse-all search with an empty query, with no CSRF seed or retry', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { items: [], page: 1, pageSize: 20, total: 0 }))

    const result = await jobPostingsClient.search('', 1, 20)

    expect(result).toEqual({ items: [], page: 1, pageSize: 20, total: 0 })
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(urlOf(0)).toBe('/api/job-postings?query=&page=1&pageSize=20')
  })
})
