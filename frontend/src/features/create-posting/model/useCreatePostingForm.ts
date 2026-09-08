import { useId, useState, type FormEvent } from 'react'

import { useMutation } from '@tanstack/react-query'

import { jobPostingsClient } from '../../../entities'
import { toApiError } from '../../../shared/lib'

type FieldName = 'title' | 'description'
type FieldErrors = Partial<Record<FieldName, string>>

/** The same length caps Story 2.1a's `[StringLength]` enforces server-side.
 * Exported so the form can set the matching native `maxLength` on each control. */
export const TITLE_MAX = 200
export const DESCRIPTION_MAX = 4000

/** Prescribed copy — used verbatim, do not reword. */
const PUBLISH_FAILED_MESSAGE = "We couldn't publish this posting. Please try again."

function validateTitle(value: string): string | undefined {
  const trimmed = value.trim()
  if (trimmed === '') return 'Enter a title for this posting.'
  if (trimmed.length > TITLE_MAX) return `The title must be ${TITLE_MAX} characters or fewer.`
  return undefined
}

function validateDescription(value: string): string | undefined {
  const trimmed = value.trim()
  if (trimmed === '') return 'Enter a description for this posting.'
  if (trimmed.length > DESCRIPTION_MAX)
    return `The description must be ${DESCRIPTION_MAX} characters or fewer.`
  return undefined
}

const VALIDATORS: Record<FieldName, (value: string) => string | undefined> = {
  title: validateTitle,
  description: validateDescription,
}

/**
 * Map a server-side `errors` map (a rule the client check missed) onto our
 * field slots by matching the key substring. Story 2.1a keys the entries
 * `Title` / `Description`.
 */
function mapServerFieldErrors(errors: Record<string, string[]>): FieldErrors {
  const mapped: FieldErrors = {}
  for (const [key, messages] of Object.entries(errors)) {
    const message = messages[0]
    if (!message) continue
    const lower = key.toLowerCase()
    if (lower.includes('title')) mapped.title = message
    else if (lower.includes('description')) mapped.description = message
  }
  return mapped
}

/**
 * The Post-a-Job form state, mirroring `useAuthForm`: `values`, per-field
 * `fieldErrors`, a form-level `formError` banner, and a `published` flag that
 * swaps the form for the confirmation panel.
 *
 * Validation runs on blur (per field) and again on submit — never per
 * keystroke; a stale error is cleared as the field is edited. On a successful
 * `create` the form is replaced by the confirmation in place (no navigation —
 * Story 2.2 owns the redirect to the posting's detail view). On a `400` with an
 * `errors` map the entries render under their fields; any other rejection shows
 * the banner with the entered values retained so a retry re-submits them.
 */
export function useCreatePostingForm() {
  const baseId = useId()

  const [values, setValues] = useState({ title: '', description: '' })
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | undefined>(undefined)
  const [published, setPublished] = useState(false)

  const mutation = useMutation({
    mutationFn: (): Promise<unknown> =>
      jobPostingsClient.create({
        title: values.title.trim(),
        description: values.description.trim(),
      }),
    onSuccess: () => {
      setPublished(true)
    },
    onError: (error: unknown) => {
      const apiError = toApiError(error)
      if (apiError?.status === 400 && apiError.errors) {
        const mapped = mapServerFieldErrors(apiError.errors)
        if (Object.keys(mapped).length > 0) {
          setFieldErrors((prev) => ({ ...prev, ...mapped }))
          return
        }
      }
      // Network / server / auth failure — keep `values` so the retry re-sends.
      setFormError(PUBLISH_FAILED_MESSAGE)
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
    setFieldErrors((prev) => ({ ...prev, [field]: VALIDATORS[field](values[field]) }))
  }

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    // Guard against a fast double-click landing a second `create` before the
    // button disables — `POST /api/job-postings` is not idempotent.
    if (mutation.isPending) return
    setFormError(undefined)

    const nextErrors: FieldErrors = {
      title: validateTitle(values.title),
      description: validateDescription(values.description),
    }
    setFieldErrors(nextErrors)

    if (nextErrors.title || nextErrors.description) return
    mutation.mutate()
  }

  /** Clear everything — backs the confirmation panel's "Post another job". */
  const reset = () => {
    setValues({ title: '', description: '' })
    setFieldErrors({})
    setFormError(undefined)
    setPublished(false)
  }

  return {
    values,
    fieldErrors,
    formError,
    published,
    isSubmitting: mutation.isPending,
    ids: {
      title: `${baseId}-title`,
      description: `${baseId}-description`,
      formError: `${baseId}-form-error`,
    },
    setField,
    blurField,
    handleSubmit,
    reset,
  }
}
