import { describe, expect, it } from 'vitest'

import { EMAIL_PATTERN, mapAuthFieldErrors, validateEmail, validateName, validatePassword } from './authValidation'

describe('validateName', () => {
  it('requires a Job Seeker full name', () => {
    expect(validateName('', 'jobSeeker')).toBe('Enter your full name.')
    expect(validateName('   ', 'jobSeeker')).toBe('Enter your full name.')
  })

  it('requires a Company name', () => {
    expect(validateName('', 'company')).toBe('Enter your company name.')
  })

  it('passes a non-blank value for either role', () => {
    expect(validateName('Priya Raman', 'jobSeeker')).toBeUndefined()
    expect(validateName('Acme Inc', 'company')).toBeUndefined()
  })
})

describe('validateEmail', () => {
  it('requires a value', () => {
    expect(validateEmail('')).toBe('Enter your email address.')
    expect(validateEmail('   ')).toBe('Enter your email address.')
  })

  it('rejects a malformed address', () => {
    expect(validateEmail('not-an-email')).toBe('Enter a valid email address.')
  })

  it('accepts a well-formed address, trimmed', () => {
    expect(validateEmail('  priya@seeker.test  ')).toBeUndefined()
  })

  it('matches the exported pattern directly', () => {
    expect(EMAIL_PATTERN.test('priya@seeker.test')).toBe(true)
    expect(EMAIL_PATTERN.test('not-an-email')).toBe(false)
  })
})

describe('validatePassword', () => {
  it('requires a value', () => {
    expect(validatePassword('')).toBe('Enter your password.')
  })

  it('requires at least 8 characters', () => {
    expect(validatePassword('short1')).toBe('Your password must be at least 8 characters.')
  })

  it('accepts a password of 8 or more characters', () => {
    expect(validatePassword('password123')).toBeUndefined()
  })
})

describe('mapAuthFieldErrors', () => {
  it('maps a key containing "name" to the name slot', () => {
    expect(mapAuthFieldErrors({ Name: ['Enter a valid name.'] })).toEqual({
      name: 'Enter a valid name.',
    })
  })

  it('maps a key containing "email" to the email slot', () => {
    expect(mapAuthFieldErrors({ Email: ['That email address is not permitted.'] })).toEqual({
      email: 'That email address is not permitted.',
    })
  })

  it('maps a key containing "password" to the password slot', () => {
    expect(mapAuthFieldErrors({ Password: ['Password is too weak.'] })).toEqual({
      password: 'Password is too weak.',
    })
  })

  it('ignores an unrecognised key and an empty message list', () => {
    expect(mapAuthFieldErrors({ Unrelated: ['x'], Email: [] })).toEqual({})
  })

  it('maps multiple recognised keys at once', () => {
    expect(
      mapAuthFieldErrors({
        Email: ['That email address is not permitted.'],
        Password: ['Password is too weak.'],
      }),
    ).toEqual({
      email: 'That email address is not permitted.',
      password: 'Password is too weak.',
    })
  })
})
