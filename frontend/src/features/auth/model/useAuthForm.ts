import { useId, useState, type FormEvent } from 'react'

import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'

import { authClient, sessionQueryKey, toApiError } from '../../../entities'
import type { Role } from '../../../shared/ui'

export type AuthMode = 'signUp' | 'logIn'

type FieldName = 'name' | 'email' | 'password'
type FieldErrors = Partial<Record<FieldName, string>>

/** Prescribed copy — used verbatim, do not reword. */
const DUPLICATE_EMAIL_MESSAGE = 'This email is already registered as a Company.'
const CREDENTIAL_MISMATCH_MESSAGE = "That email and password don't match. Please try again."
/** Fallback for any other failure. Formal, complete sentence, no exclamation. */
const GENERIC_MESSAGE = 'We could not complete your request. Please try again.'

// A deliberately permissive check — the server's `[EmailAddress]` rule is
// authoritative; this only catches the obviously malformed before a request.
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

function validateName(value: string): string | undefined {
  return value.trim() === '' ? 'Enter your company name.' : undefined
}

function validateEmail(value: string): string | undefined {
  const trimmed = value.trim()
  if (trimmed === '') return 'Enter your email address.'
  if (!EMAIL_PATTERN.test(trimmed)) return 'Enter a valid email address.'
  return undefined
}

function validatePassword(value: string): string | undefined {
  if (value === '') return 'Enter your password.'
  if (value.length < 8) return 'Your password must be at least 8 characters.'
  return undefined
}

const VALIDATORS: Record<FieldName, (value: string) => string | undefined> = {
  name: validateName,
  email: validateEmail,
  password: validatePassword,
}

/**
 * Map a server-side `errors` map (a rule the client check missed) onto our
 * field slots by matching the key substring.
 */
function mapServerFieldErrors(errors: Record<string, string[]>): FieldErrors {
  const mapped: FieldErrors = {}
  for (const [key, messages] of Object.entries(errors)) {
    const message = messages[0]
    if (!message) continue
    const lower = key.toLowerCase()
    if (lower.includes('name')) mapped.name = message
    else if (lower.includes('email')) mapped.email = message
    else if (lower.includes('password')) mapped.password = message
  }
  return mapped
}

export function useAuthForm() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const baseId = useId()

  const [mode, setMode] = useState<AuthMode>('signUp')
  const [role, setRole] = useState<Role>('company')
  const [values, setValues] = useState({ name: '', email: '', password: '' })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | undefined>(undefined)

  const mutation = useMutation({
    mutationFn: (): Promise<unknown> => {
      if (mode === 'signUp') {
        return authClient.register({
          accountType: 'company',
          name: values.name.trim(),
          email: values.email.trim(),
          password: values.password,
        })
      }
      return authClient.login({
        accountType: 'company',
        email: values.email.trim(),
        password: values.password,
      })
    },
    onSuccess: () => {
      // Fire-and-forget: `me` is invalidated so the shell refetches, but the
      // navigation must not wait on that round-trip.
      void queryClient.invalidateQueries({ queryKey: sessionQueryKey })
      navigate('/')
    },
    onError: (error: unknown) => {
      const apiError = toApiError(error)
      if (apiError?.status === 409) {
        setFieldErrors((prev) => ({ ...prev, email: DUPLICATE_EMAIL_MESSAGE }))
        return
      }
      if (apiError?.status === 401) {
        setFormError(CREDENTIAL_MISMATCH_MESSAGE)
        return
      }
      if (apiError?.status === 400 && apiError.errors) {
        const mapped = mapServerFieldErrors(apiError.errors)
        if (Object.keys(mapped).length > 0) {
          setFieldErrors((prev) => ({ ...prev, ...mapped }))
          return
        }
      }
      setFormError(GENERIC_MESSAGE)
    },
  })

  const setField = (field: FieldName, value: string) => {
    setValues((prev) => ({ ...prev, [field]: value }))
    // Validation is blur/submit only — never per keystroke — but a stale error
    // is cleared as the user edits the field.
    setFieldErrors((prev) => (prev[field] ? { ...prev, [field]: undefined } : prev))
    setFormError(undefined)
  }

  const blurField = (field: FieldName) => {
    if (field === 'name' && mode !== 'signUp') return
    setFieldErrors((prev) => ({ ...prev, [field]: VALIDATORS[field](values[field]) }))
  }

  const switchMode = () => {
    setMode((prev) => (prev === 'signUp' ? 'logIn' : 'signUp'))
    setFieldErrors({})
    setFormError(undefined)
  }

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormError(undefined)

    const nextErrors: FieldErrors = {}
    if (mode === 'signUp') nextErrors.name = validateName(values.name)
    nextErrors.email = validateEmail(values.email)
    nextErrors.password = validatePassword(values.password)
    setFieldErrors(nextErrors)

    if (nextErrors.name || nextErrors.email || nextErrors.password) return
    mutation.mutate()
  }

  return {
    mode,
    role,
    values,
    fieldErrors,
    formError,
    isSubmitting: mutation.isPending,
    ids: {
      name: `${baseId}-name`,
      email: `${baseId}-email`,
      password: `${baseId}-password`,
      formError: `${baseId}-form-error`,
    },
    setRole,
    setField,
    blurField,
    switchMode,
    handleSubmit,
  }
}
