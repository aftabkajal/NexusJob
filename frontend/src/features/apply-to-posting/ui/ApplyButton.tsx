import { useEffect, useId, useRef } from 'react'

import { APPLY_SUBMITTED_MESSAGE, useApplyToPosting } from '../model/useApplyToPosting'

import styles from './ApplyButton.module.css'

/**
 * The apply surface for the posting detail page: the single accent-colored
 * primary action, in three states —
 *
 * - **Apply** (enabled, accent): a signed-in Job Seeker who has not applied.
 * - **Applied** (disabled, `--color-border` / `--color-text-secondary`): after a
 *   fresh inline submit, or straight away on load if `GET /api/applications/mine`
 *   says they already applied.
 * - **hidden** (`null`): a signed-out visitor, a Company, or an unresolved
 *   session — no button, no stopgap (Story 3.2 adds the signed-out branch).
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

  const errorId = useId()
  const confirmationRef = useRef<HTMLParagraphElement>(null)

  useEffect(() => {
    if (showConfirmation) {
      confirmationRef.current?.focus()
    }
  }, [showConfirmation])

  if (render === 'hidden') {
    return null
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
