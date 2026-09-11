import { useId, useState, type FormEvent } from 'react'

import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'

import { authClient, sessionQueryKey } from '../../../entities'
import {
  mapAuthFieldErrors,
  toApiError,
  validateEmail,
  validateName,
  validatePassword,
  type AuthFieldErrors,
} from '../../../shared/lib'
import type { Role } from '../../../shared/ui'

export type AuthMode = 'signUp' | 'logIn'

type FieldName = 'name' | 'email' | 'password'
type FieldErrors = AuthFieldErrors

/** Prescribed copy — used verbatim, do not reword. Role-aware: names the role
 * the email is already taken by, matching the epic microcopy rules. */
function duplicateEmailMessage(role: Role): string {
  return role === 'jobSeeker'
    ? 'This email is already registered as a Job Seeker.'
    : 'This email is already registered as a Company.'
}
const CREDENTIAL_MISMATCH_MESSAGE = "That email and password don't match. Please try again."
/** Fallback for any other failure. Formal, complete sentence, no exclamation. */
const GENERIC_MESSAGE = 'We could not complete your request. Please try again.'

const VALIDATORS: Record<'email' | 'password', (value: string) => string | undefined> = {
  email: validateEmail,
  password: validatePassword,
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
      // The single Role → API request wire-value translation. `job_seeker` never
      // escapes this closure on the request side — no component prop or the
      // shell knows it (the response `accountType` is mapped in `fetchSession`).
      const accountType = role === 'jobSeeker' ? 'job_seeker' : 'company'
      if (mode === 'signUp') {
        return authClient.register({
          accountType,
          name: values.name.trim(),
          email: values.email.trim(),
          password: values.password,
        })
      }
      return authClient.login({
        accountType,
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
        setFieldErrors((prev) => ({ ...prev, email: duplicateEmailMessage(role) }))
        return
      }
      if (apiError?.status === 401) {
        setFormError(CREDENTIAL_MISMATCH_MESSAGE)
        return
      }
      if (apiError?.status === 400 && apiError.errors) {
        const mapped = mapAuthFieldErrors(apiError.errors)
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
    const message =
      field === 'name' ? validateName(values.name, role) : VALIDATORS[field](values[field])
    setFieldErrors((prev) => ({ ...prev, [field]: message }))
  }

  const switchMode = () => {
    setMode((prev) => (prev === 'signUp' ? 'logIn' : 'signUp'))
    setFieldErrors({})
    setFormError(undefined)
  }

  /**
   * Switch the active role. Clears every field + form error (the entered name /
   * email / password are kept) — a faithful, simpler reading of the 1.4 AC's
   * "role-specific field errors", matching how `switchMode` already behaves.
   *
   * A no-op when the role is unchanged (`RoleToggle` fires `onChange` with the
   * already-active role on a click / Enter / Space — selection follows focus)
   * or while a request is in flight (so an in-flight `409`'s `onError` still
   * labels the account type that was actually submitted).
   */
  const changeRole = (next: Role) => {
    if (next === role || mutation.isPending) return
    setRole(next)
    setFieldErrors({})
    setFormError(undefined)
  }

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setFormError(undefined)

    const nextErrors: FieldErrors = {}
    if (mode === 'signUp') nextErrors.name = validateName(values.name, role)
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
    changeRole,
    setField,
    blurField,
    switchMode,
    handleSubmit,
  }
}
