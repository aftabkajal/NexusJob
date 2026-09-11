import { useId, useState, type FormEvent } from 'react'

import { useMutation } from '@tanstack/react-query'

import { authClient } from '../../../entities'
import {
  mapAuthFieldErrors,
  toApiError,
  validateEmail,
  validateName,
  validatePassword,
  type AuthFieldErrors,
} from '../../../shared/lib'
import type { Role } from '../../../shared/ui'

type FieldName = 'name' | 'email' | 'password'
type FieldErrors = AuthFieldErrors

/** Prescribed copy — used verbatim, do not reword. The gate is Job-Seeker-only
 * (Decision, 2026-09-11: register-only, no log-in path), so the duplicate-email
 * message never varies by role the way `useAuthForm`'s does. */
const DUPLICATE_EMAIL_MESSAGE = 'This email is already registered as a Job Seeker.'
/** Fallback for any other failure. Formal, complete sentence, no exclamation. */
const GENERIC_MESSAGE = 'We could not complete your request. Please try again.'

const VALIDATORS: Record<'email' | 'password', (value: string) => string | undefined> = {
  email: validateEmail,
  password: validatePassword,
}

/** The apply-gate is fixed to Job Seeker — never a toggle state, only
 * `RoleToggle`'s `disabledValues` prop varies. */
const ROLE: Role = 'jobSeeker'

/**
 * The apply-gate modal's mini sign-up form state — register-only, adapted from
 * `useAuthForm`'s sign-up branch (not imported: FSD forbids
 * `features/apply-to-posting` importing `features/auth`, and `useAuthForm`'s
 * `onSuccess` hardcodes `navigate('/')`, which must never fire here).
 *
 * On a successful register, calls `onAuthenticated` — the parent
 * (`ApplyButton`) invalidates the session and fires the existing
 * `useApplyToPosting().apply()`. This hook never touches the applications
 * endpoint itself.
 */
export function useApplyGateForm(onAuthenticated: () => void) {
  const baseId = useId()

  const [values, setValues] = useState({ name: '', email: '', password: '' })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | undefined>(undefined)

  const mutation = useMutation({
    mutationFn: () =>
      authClient.register({
        accountType: 'job_seeker',
        name: values.name.trim(),
        email: values.email.trim(),
        password: values.password,
      }),
    onSuccess: () => {
      onAuthenticated()
    },
    onError: (error: unknown) => {
      const apiError = toApiError(error)
      if (apiError?.status === 409) {
        setFieldErrors((prev) => ({ ...prev, email: DUPLICATE_EMAIL_MESSAGE }))
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
    const message =
      field === 'name' ? validateName(values.name, ROLE) : VALIDATORS[field](values[field])
    setFieldErrors((prev) => ({ ...prev, [field]: message }))
  }

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    // Guard against a fast double-submit (e.g. pressing Enter twice) landing
    // a second `register` before the button's `disabled={isSubmitting}`
    // re-render commits — mirrors `useCreatePostingForm.handleSubmit` and
    // `useApplyToPosting.apply()`'s identical guard.
    if (mutation.isPending) return
    setFormError(undefined)

    const nextErrors: FieldErrors = {
      name: validateName(values.name, ROLE),
      email: validateEmail(values.email),
      password: validatePassword(values.password),
    }
    setFieldErrors(nextErrors)

    if (nextErrors.name || nextErrors.email || nextErrors.password) return
    mutation.mutate()
  }

  return {
    role: ROLE,
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
    setField,
    blurField,
    handleSubmit,
  }
}
