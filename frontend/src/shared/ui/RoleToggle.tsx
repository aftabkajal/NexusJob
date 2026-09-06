import { useId, useRef, useState, type KeyboardEvent } from 'react'

import styles from './RoleToggle.module.css'

export type Role = 'company' | 'jobSeeker'

interface RoleOption {
  value: Role
  label: string
}

/** Display order in the pill. Default selection is the last entry, "Job Seeker". */
const OPTIONS: readonly RoleOption[] = [
  { value: 'company', label: 'Company' },
  { value: 'jobSeeker', label: 'Job Seeker' },
]

const DEFAULT_ROLE: Role = 'jobSeeker'

const NEXT_KEYS = new Set(['ArrowRight', 'ArrowDown'])
const PREV_KEYS = new Set(['ArrowLeft', 'ArrowUp'])
const CONFIRM_KEYS = new Set(['Enter', ' '])

export interface RoleToggleProps {
  /** Controlled selection. Omit for uncontrolled use seeded by `defaultValue`. */
  value?: Role
  /** Initial selection when uncontrolled. Defaults to "Job Seeker". */
  defaultValue?: Role
  /** Fires with the newly selected role on arrow-key move, confirm, or click. */
  onChange?: (role: Role) => void
  /** Accessible name for the radiogroup. */
  label?: string
}

/**
 * `auth-role-toggle` — a two-option pill switch ("Company" / "Job Seeker").
 *
 * Presentational only: a controlled/uncontrolled radiogroup with no domain
 * knowledge. Story 1.3 composes it into the auth surface and supplies
 * `onChange`. Selection follows focus (WAI-ARIA radio-group pattern): arrow keys
 * move the active option and fire `onChange`; `Enter`/`Space` re-confirm the
 * focused option. Roving `tabindex` keeps a single tab stop. The active option
 * carries the `primary` fill and the `full` (pill) radius.
 */
export function RoleToggle({
  value,
  defaultValue,
  onChange,
  label = 'Select account type',
}: RoleToggleProps) {
  const isControlled = value !== undefined
  const [internal, setInternal] = useState<Role>(defaultValue ?? DEFAULT_ROLE)
  const selected: Role = isControlled ? value : internal
  const groupId = useId()
  const optionRefs = useRef<Array<HTMLButtonElement | null>>([])

  const selectedIndex = OPTIONS.findIndex((option) => option.value === selected)

  const commit = (role: Role) => {
    if (!isControlled) {
      setInternal(role)
    }
    const index = OPTIONS.findIndex((option) => option.value === role)
    optionRefs.current[index]?.focus()
    onChange?.(role)
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (NEXT_KEYS.has(event.key)) {
      event.preventDefault()
      commit(OPTIONS[(selectedIndex + 1) % OPTIONS.length].value)
      return
    }
    if (PREV_KEYS.has(event.key)) {
      event.preventDefault()
      commit(OPTIONS[(selectedIndex - 1 + OPTIONS.length) % OPTIONS.length].value)
      return
    }
    if (CONFIRM_KEYS.has(event.key)) {
      event.preventDefault()
      commit(selected)
    }
  }

  return (
    <div role="radiogroup" aria-label={label} className={styles.track} onKeyDown={handleKeyDown}>
      {OPTIONS.map((option, index) => {
        const isSelected = option.value === selected
        const className = isSelected
          ? `${styles.option} ${styles['option-active']}`
          : styles.option
        return (
          <button
            key={option.value}
            ref={(node) => {
              optionRefs.current[index] = node
            }}
            type="button"
            role="radio"
            id={`${groupId}-${option.value}`}
            aria-checked={isSelected}
            tabIndex={isSelected ? 0 : -1}
            className={className}
            onClick={() => commit(option.value)}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}
