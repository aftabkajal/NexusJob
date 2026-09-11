import { useEffect, useId, useRef, useState } from 'react'

import { useQueryClient } from '@tanstack/react-query'

import { sessionQueryKey } from '../../../entities'

import { APPLY_SUBMITTED_MESSAGE, useApplyToPosting } from '../model/useApplyToPosting'

import { ApplyGateModal } from './ApplyGateModal'
import styles from './ApplyButton.module.css'

/**
 * The apply surface for the posting detail page: the single accent-colored
 * primary action, in three states —
 *
 * - **Apply** (enabled, accent): a signed-in Job Seeker who has not applied.
 * - **Applied** (disabled, `--color-border` / `--color-text-secondary`): after a
 *   fresh inline submit, or straight away on load if `GET /api/applications/mine`
 *   says they already applied.
 * - **gate** (enabled, accent, `aria-haspopup="dialog"`): a signed-out visitor.
 *   A click opens `ApplyGateModal` instead of applying directly; a successful
 *   register closes the gate and fires the same `apply()` a signed-in Job
 *   Seeker's click already uses.
 * - **hidden** (`null`): a Company, or an unresolved session — no button, no
 *   stopgap.
 *
 * A fresh submit shows `Your application has been submitted.` in a
 * `<p role="status">` and moves focus there (a disabled button cannot hold
 * focus — mirrors 2.1b's confirmation-focus fix). An inline failure shows
 * `We couldn't submit your application. Please try again.` in a
 * `<p role="alert">`; the button re-enables as `Apply` and is the retry. No
 * navigation on success — the posting detail stays on screen.
 */
export function ApplyButton({ jobPostingId }: { jobPostingId: string }) {
  const { render, applied, showConfirmation, formError, disabled, apply } =
    useApplyToPosting(jobPostingId)
  const queryClient = useQueryClient()

  const errorId = useId()
  const confirmationRef = useRef<HTMLParagraphElement>(null)
  const [isGateOpen, setIsGateOpen] = useState(false)
  // The visitor closed the gate (Escape/scrim) before its in-flight register
  // resolved. `useMutation` does not cancel the request or skip `onSuccess` on
  // unmount, so a late-arriving success must be told not to auto-apply — the
  // account may end up created, but "Escape cancels, no application is
  // submitted" still holds. Reset whenever the gate is (re)opened.
  const cancelledRef = useRef(false)
  // Once a gate-triggered register succeeds, keep showing the normal
  // button/confirmation/error branch regardless of what `render` says —
  // `render` depends on `useSession()`'s own refetch (invalidated below),
  // a second, independent request racing the apply POST. Without this, a
  // successful register+apply can settle while `render` is still `'gate'`,
  // leaving the confirmation invisible.
  const [authenticatedViaGate, setAuthenticatedViaGate] = useState(false)

  useEffect(() => {
    if (showConfirmation) {
      confirmationRef.current?.focus()
    }
  }, [showConfirmation])

  // Computed before the `'hidden'` check, not after: if the session refetch
  // this same success triggered comes back errored (or resolves to some
  // other account entirely), raw `render` can itself become `'hidden'` —
  // `authenticatedViaGate` must override that too, or a just-submitted
  // application's confirmation disappears along with the whole surface.
  const effectiveRender = authenticatedViaGate ? 'button' : render

  if (effectiveRender === 'hidden') {
    return null
  }

  if (effectiveRender === 'gate') {
    return (
      <div className={styles.wrap}>
        <button
          type="button"
          className={styles.apply}
          aria-haspopup="dialog"
          aria-expanded={isGateOpen}
          onClick={() => {
            cancelledRef.current = false
            setIsGateOpen(true)
          }}
        >
          Apply
        </button>
        {isGateOpen && (
          <ApplyGateModal
            jobPostingId={jobPostingId}
            onClose={() => {
              cancelledRef.current = true
              setIsGateOpen(false)
            }}
            onAuthenticated={() => {
              if (cancelledRef.current) return
              setIsGateOpen(false)
              setAuthenticatedViaGate(true)
              void queryClient.invalidateQueries({ queryKey: sessionQueryKey })
              apply()
            }}
          />
        )}
      </div>
    )
  }

  return (
    <div className={styles.wrap}>
      <button
        type="button"
        className={applied ? styles.applied : styles.apply}
        disabled={disabled}
        aria-describedby={formError ? errorId : undefined}
        onClick={apply}
      >
        {applied ? 'Applied' : 'Apply'}
      </button>
      {formError && (
        <p id={errorId} className={styles.banner} role="alert">
          {formError}
        </p>
      )}
      {showConfirmation && (
        <p className={styles.confirmation} role="status" tabIndex={-1} ref={confirmationRef}>
          {APPLY_SUBMITTED_MESSAGE}
        </p>
      )}
    </div>
  )
}
