import { useState } from 'react'

import { useMutation, useQueryClient } from '@tanstack/react-query'

import {
  applicationMineQueryKey,
  applicationsClient,
  useMyApplication,
  useSession,
} from '../../../entities'

/** Prescribed copy — module-level, used verbatim, do not reword. */
const APPLY_FAILED_MESSAGE = "We couldn't submit your application. Please try again."
export const APPLY_SUBMITTED_MESSAGE = 'Your application has been submitted.'

export type ApplyRender = 'hidden' | 'button'

export interface UseApplyToPosting {
  /** `hidden` for a signed-out / Company / unresolved session — the button
   * renders nothing (Story 3.2 adds the signed-out branch and its modal). */
  render: ApplyRender
  /** `true` once the Job Seeker has applied — on load (`mine.applied`) or after
   * a fresh inline submit (`submitted`). Drives the `Applied` label + styling. */
  applied: boolean
  /** `true` only after a fresh inline submit — the on-load already-applied
   * state shows no confirmation `<p>`. */
  showConfirmation: boolean
  /** The inline failure message, or `undefined`. */
  formError: string | undefined
  /** `true` while the `apply` POST is in flight. */
  isSubmitting: boolean
  /** `true` while the `mine` query is unsettled, while applied, or while
   * submitting — the button is non-interactive in every one of those. */
  disabled: boolean
  /** Fire the inline apply. A no-op while a request is already in flight (the
   * double-click guard — although 3.1a is idempotent). */
  apply: () => void
}

/**
 * The apply surface's state for the posting detail page — the AR-10 tension
 * point: it composes `entities/session` (who is viewing) and the posting id (an
 * `entities/job-posting` concern), so it must sit in `features/`, not
 * `entities/application/ui`.
 *
 * Mirrors `useCreatePostingForm`: a `useMutation` whose `mutationFn` calls the
 * slice client, `mutation.isPending` guarding a double-click, `onSuccess`
 * invalidating the owned query key and flipping local state, `onError` setting
 * an inline `formError`. No navigation on success — the posting detail stays on
 * screen.
 */
export function useApplyToPosting(jobPostingId: string): UseApplyToPosting {
  const queryClient = useQueryClient()
  const session = useSession()
  // A single gate for both the `mine` query and `render`, so they cannot
  // diverge: only a resolved, error-free session that is a Job Seeker enables
  // either. `session.isError` with a retained `data.kind === 'jobSeeker'` would
  // otherwise fire the 401/403-only `mine` GET while the button stays hidden.
  const isJobSeeker = !session.isPending && !session.isError && session.data?.kind === 'jobSeeker'
  const mine = useMyApplication(jobPostingId, isJobSeeker)

  const [submitted, setSubmitted] = useState(false)
  const [formError, setFormError] = useState<string>()

  const mutation = useMutation({
    mutationFn: () => applicationsClient.apply({ jobPostingId }),
    onSuccess: () => {
      setSubmitted(true)
      setFormError(undefined)
      void queryClient.invalidateQueries({ queryKey: applicationMineQueryKey(jobPostingId) })
    },
    onError: () => {
      setFormError(APPLY_FAILED_MESSAGE)
    },
  })

  // The local `submitted` flag flips the UI synchronously on the 200 so there is
  // no window where the button shows `Apply` again between success and the
  // `mine` refetch. Branch on `.applied`, never on `appliedAt`.
  const applied = submitted || mine.data?.applied === true

  const render: ApplyRender = isJobSeeker ? 'button' : 'hidden'

  return {
    render,
    applied,
    showConfirmation: submitted,
    formError,
    isSubmitting: mutation.isPending,
    disabled: mine.isPending || applied || mutation.isPending,
    apply: () => {
      if (mutation.isPending) return
      // Clear a stale failure banner at the start of a retry, mirroring
      // `useCreatePostingForm.handleSubmit` — otherwise the old
      // `role="alert"` message (and its `aria-describedby`) persists through
      // the in-flight window.
      setFormError(undefined)
      mutation.mutate()
    },
  }
}
