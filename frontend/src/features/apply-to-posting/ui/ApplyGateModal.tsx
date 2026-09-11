import { useId, useRef } from 'react'

import { Modal, RoleToggle } from '../../../shared/ui'

import { useApplyGateForm } from '../model/useApplyGateForm'

import styles from './ApplyGateModal.module.css'

export interface ApplyGateModalProps {
  /** Accepted for symmetry with the rest of the feature's components — this
   * component never calls the applications endpoint itself; the parent does,
   * via `onAuthenticated`. */
  jobPostingId: string
  onClose: () => void
  /** Fires once, after a successful register. */
  onAuthenticated: () => void
}

/**
 * The apply-gate's mini sign-up form, rendered inside `shared/ui/Modal` — the
 * Job-Seeker-fixed register surface a signed-out visitor sees on clicking
 * `Apply`. Field markup/validation-on-blur pattern adapted from
 * `features/auth/ui/AuthForm.tsx` (not imported — FSD forbids a
 * `features/*` sibling import).
 *
 * No log-in path this story (Decision, 2026-09-11): register-only, no
 * mode/switchMode state, no 401 credential-mismatch banner.
 */
export function ApplyGateModal({ onClose, onAuthenticated }: ApplyGateModalProps) {
  const titleId = useId()
  const nameFieldRef = useRef<HTMLInputElement>(null)

  const { values, fieldErrors, formError, isSubmitting, ids, setField, blurField, handleSubmit } =
    useApplyGateForm(onAuthenticated)

  return (
    <Modal titleId={titleId} onClose={onClose} initialFocusRef={nameFieldRef}>
      <form className={styles.card} onSubmit={handleSubmit} noValidate>
        <h2 id={titleId} className={styles.heading}>
          Create your account
        </h2>

        <RoleToggle value="jobSeeker" disabledValues={['company']} label="Select account type" />

        <div className={styles.field}>
          <label htmlFor={ids.name} className={styles.label}>
            Full name
          </label>
          <input
            id={ids.name}
            name="name"
            type="text"
            autoComplete="name"
            ref={nameFieldRef}
            className={fieldErrors.name ? `${styles.input} ${styles['input-invalid']}` : styles.input}
            value={values.name}
            onChange={(event) => setField('name', event.target.value)}
            onBlur={() => blurField('name')}
            aria-invalid={fieldErrors.name ? true : undefined}
            aria-describedby={fieldErrors.name ? `${ids.name}-error` : undefined}
          />
          {fieldErrors.name && (
            <p id={`${ids.name}-error`} className={styles.error}>
              {fieldErrors.name}
            </p>
          )}
        </div>

        <div className={styles.field}>
          <label htmlFor={ids.email} className={styles.label}>
            Email
          </label>
          <input
            id={ids.email}
            name="email"
            type="email"
            autoComplete="email"
            className={fieldErrors.email ? `${styles.input} ${styles['input-invalid']}` : styles.input}
            value={values.email}
            onChange={(event) => setField('email', event.target.value)}
            onBlur={() => blurField('email')}
            aria-invalid={fieldErrors.email ? true : undefined}
            aria-describedby={fieldErrors.email ? `${ids.email}-error` : undefined}
          />
          {fieldErrors.email && (
            <p id={`${ids.email}-error`} className={styles.error}>
              {fieldErrors.email}
            </p>
          )}
        </div>

        <div className={styles.field}>
          <label htmlFor={ids.password} className={styles.label}>
            Password
          </label>
          <input
            id={ids.password}
            name="password"
            type="password"
            autoComplete="new-password"
            className={
              fieldErrors.password ? `${styles.input} ${styles['input-invalid']}` : styles.input
            }
            value={values.password}
            onChange={(event) => setField('password', event.target.value)}
            onBlur={() => blurField('password')}
            aria-invalid={fieldErrors.password ? true : undefined}
            aria-describedby={fieldErrors.password ? `${ids.password}-error` : undefined}
          />
          {fieldErrors.password && (
            <p id={`${ids.password}-error`} className={styles.error}>
              {fieldErrors.password}
            </p>
          )}
        </div>

        {formError && (
          <p id={ids.formError} className={styles['form-error']} role="alert">
            {formError}
          </p>
        )}

        <button
          type="submit"
          className={styles.submit}
          disabled={isSubmitting}
          aria-describedby={formError ? ids.formError : undefined}
        >
          Create account
        </button>
      </form>
    </Modal>
  )
}
