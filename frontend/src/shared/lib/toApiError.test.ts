import { describe, expect, it } from 'vitest'

import { ApiException } from '../api/nexus-api-client'

import { toApiError } from './toApiError'

describe('toApiError', () => {
  it('maps an ApiException to its status and message', () => {
    const err = new ApiException('Bad Request', 400, '', {}, null)

    expect(toApiError(err)).toEqual({ status: 400, title: 'Bad Request' })
  })

  it('passes a ProblemDetails-shaped object through, carrying title and errors', () => {
    const body = {
      status: 400,
      title: 'One or more validation errors occurred.',
      errors: { Title: ['The Title field is required.'] },
    }

    expect(toApiError(body)).toEqual({
      status: 400,
      title: 'One or more validation errors occurred.',
      errors: { Title: ['The Title field is required.'] },
    })
  })

  it('maps a bodiless ProblemDetails object (status only)', () => {
    expect(toApiError({ status: 401 })).toEqual({
      status: 401,
      title: undefined,
      errors: undefined,
    })
  })

  it('returns null for anything without a numeric status', () => {
    expect(toApiError(new Error('boom'))).toBeNull()
    expect(toApiError({ message: 'network failure' })).toBeNull()
    expect(toApiError('nope')).toBeNull()
    expect(toApiError(null)).toBeNull()
    expect(toApiError(undefined)).toBeNull()
  })
})
