import { RoleToggle } from '../../../shared/ui'

import { useAuthForm } from '../model/useAuthForm'

import styles from './AuthForm.module.css'

/**
 * The role-toggle sign-up / log-in surface for Company accounts.
 *
 * Sign-up mode adds a required "Company name" field above email; log-in mode
 * never shows it. The role toggle defaults to Company with Job Seeker disabled
 * (story 1.4 re-enables it). Validation runs on blur and again on submit —
 * never per keystroke — and every field error is `aria-describedby`-linked.
 */
export function AuthForm() {
  const {
    mode,
    role,
    values,
    fieldErrors,
    formError,
    isSubmitting,
    ids,
    setRole,
    setField,
    blurField,
    switchMode,
    handleSubmit,
  } = useAuthForm()

  const isSignUp = mode === 'signUp'

  return (
    <form className={styles.card} onSubmit={handleSubmit} noValidate>
      <h1 className={styles.heading}>{isSignUp ? 'Create your account' : 'Log in'}</h1>

      <RoleToggle
        label="Select account type"
        value={role}
        onChange={setRole}
        disabledValues={['jobSeeker']}
      />
      <p className={styles.note}>Job Seeker accounts are coming soon.</p>

      {isSignUp && (
        <div className={styles.field}>
          <label htmlFor={ids.name} className={styles.label}>
            Company name
          </label>
          <input
            id={ids.name}
            name="name"
            type="text"
            autoComplete="organization"
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
      )}

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
          autoComplete={isSignUp ? 'new-password' : 'current-password'}
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
        {isSignUp ? 'Create account' : 'Log in'}
      </button>

      <p className={styles['mode-switch']}>
        {isSignUp ? 'Already have an account? ' : 'New to NexusJob? '}
        <button
          type="button"
          className={styles['mode-switch-action']}
          onClick={switchMode}
          disabled={isSubmitting}
        >
          {isSignUp ? 'Log in' : 'Sign up'}
        </button>
      </p>
    </form>
  )
}
