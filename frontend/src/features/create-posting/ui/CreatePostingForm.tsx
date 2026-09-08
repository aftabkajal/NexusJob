import { useEffect, useRef } from 'react'

import {
  DESCRIPTION_MAX,
  TITLE_MAX,
  useCreatePostingForm,
} from '../model/useCreatePostingForm'

import styles from './CreatePostingForm.module.css'

/**
 * The Post-a-Job surface for a signed-in Company: a required title `<input>`, a
 * required description `<textarea>`, and a single Publish action rendered as the
 * `apply-button` (solid `--color-accent`) — the one primary action here.
 *
 * On a successful publish the form is replaced in place by a `--color-success`
 * confirmation panel with a "Post another job" affordance that resets the form
 * (Story 2.2 will change this to a redirect to the new posting's detail view).
 * Validation runs on blur and again on submit — never per keystroke — and every
 * field error is `aria-describedby`-linked; a network / server failure shows the
 * `--color-danger` banner with the entered values retained.
 */
export function CreatePostingForm() {
  const {
    values,
    fieldErrors,
    formError,
    published,
    isSubmitting,
    ids,
    setField,
    blurField,
    handleSubmit,
    reset,
  } = useCreatePostingForm()

  const confirmationRef = useRef<HTMLParagraphElement>(null)
  const titleRef = useRef<HTMLInputElement>(null)
  const hasPublished = useRef(false)

  // Move focus onto the confirmation when the publish succeeds (so the removed
  // Publish button does not strand keyboard focus, and the `role="status"` node
  // is reliably announced); return it to the Title field on "Post another job".
  useEffect(() => {
    if (published) {
      hasPublished.current = true
      confirmationRef.current?.focus()
    } else if (hasPublished.current) {
      hasPublished.current = false
      titleRef.current?.focus()
    }
  }, [published])

  if (published) {
    return (
      <div className={styles.card}>
        <p className={styles.confirmation} role="status" tabIndex={-1} ref={confirmationRef}>
          Your job posting has been published.
        </p>
        <button type="button" className={styles.submit} onClick={reset}>
          Post another job
        </button>
      </div>
    )
  }

  return (
    <form className={styles.card} onSubmit={handleSubmit} noValidate>
      <h1 className={styles.heading}>Post a job</h1>

      <div className={styles.field}>
        <label htmlFor={ids.title} className={styles.label}>
          Title
        </label>
        <input
          id={ids.title}
          name="title"
          type="text"
          ref={titleRef}
          maxLength={TITLE_MAX}
          className={
            fieldErrors.title ? `${styles.input} ${styles['input-invalid']}` : styles.input
          }
          value={values.title}
          onChange={(event) => setField('title', event.target.value)}
          onBlur={() => blurField('title')}
          aria-invalid={fieldErrors.title ? true : undefined}
          aria-describedby={fieldErrors.title ? `${ids.title}-error` : undefined}
        />
        {fieldErrors.title && (
          <p id={`${ids.title}-error`} className={styles.error}>
            {fieldErrors.title}
          </p>
        )}
      </div>

      <div className={styles.field}>
        <label htmlFor={ids.description} className={styles.label}>
          Description
        </label>
        <textarea
          id={ids.description}
          name="description"
          rows={8}
          maxLength={DESCRIPTION_MAX}
          className={
            fieldErrors.description
              ? `${styles.input} ${styles.textarea} ${styles['input-invalid']}`
              : `${styles.input} ${styles.textarea}`
          }
          value={values.description}
          onChange={(event) => setField('description', event.target.value)}
          onBlur={() => blurField('description')}
          aria-invalid={fieldErrors.description ? true : undefined}
          aria-describedby={fieldErrors.description ? `${ids.description}-error` : undefined}
        />
        {fieldErrors.description && (
          <p id={`${ids.description}-error`} className={styles.error}>
            {fieldErrors.description}
          </p>
        )}
      </div>

      {formError && (
        <p id={ids.formError} className={styles.banner} role="alert">
          {formError}
        </p>
      )}

      <button
        type="submit"
        className={styles.submit}
        disabled={isSubmitting}
        aria-describedby={formError ? ids.formError : undefined}
      >
        Publish
      </button>
    </form>
  )
}
