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
  /**
   * Roles that render disabled: `aria-disabled`, a muted style, skipped by
   * arrow-key navigation and ignored on click. `features/auth` passes
   * `['jobSeeker']`; story 1.4 drops the prop to re-enable it.
   */
  disabledValues?: Role[]
}

/**
 * `auth-role-toggle` — a two-option pill switch ("Company" / "Job Seeker").
 *
 * Presentational only: a controlled/uncontrolled radiogroup with no domain
 * knowledge. Selection follows focus (WAI-ARIA radio-group pattern): arrow keys
 * move the active option and fire `onChange`; `Enter`/`Space` re-confirm the
 * focused option. Roving `tabindex` keeps a single tab stop. The active option
 * carries the `primary` fill and the `full` (pill) radius. A disabled option is
 * never selectable and is stepped over by the arrow-key loop.
 */
export function RoleToggle({
  value,
  defaultValue,
  onChange,
  label = 'Select account type',
  disabledValues,
}: RoleToggleProps) {
  const isControlled = value !== undefined
  const [internal, setInternal] = useState<Role>(defaultValue ?? DEFAULT_ROLE)
  const selected: Role = isControlled ? value : internal
  const groupId = useId()
  const optionRefs = useRef<Array<HTMLButtonElement | null>>([])

  const isDisabled = (role: Role) => disabledValues?.includes(role) ?? false
  const selectedIndex = OPTIONS.findIndex((option) => option.value === selected)

  /**
   * Walk from `selectedIndex` in `step` direction to the next enabled option,
   * stopping once the scan wraps back to the start — so a toggle whose only
   * other option is disabled simply stays put.
   */
  const nextEnabledIndex = (step: number): number => {
    const count = OPTIONS.length
    let index = selectedIndex
    for (let hops = 0; hops < count; hops += 1) {
      index = (index + step + count) % count
      if (index === selectedIndex) break
      if (!isDisabled(OPTIONS[index].value)) return index
    }
    return selectedIndex
  }

  const commit = (role: Role) => {
    if (isDisabled(role)) return
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
      const next = nextEnabledIndex(1)
      if (next !== selectedIndex) commit(OPTIONS[next].value)
      return
    }
    if (PREV_KEYS.has(event.key)) {
      event.preventDefault()
      const prev = nextEnabledIndex(-1)
      if (prev !== selectedIndex) commit(OPTIONS[prev].value)
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
        const disabled = isDisabled(option.value)
        const className = [
          styles.option,
          isSelected ? styles['option-active'] : '',
          disabled ? styles['option-disabled'] : '',
        ]
          .filter(Boolean)
          .join(' ')
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
            aria-disabled={disabled ? true : undefined}
            tabIndex={isSelected ? 0 : -1}
            className={className}
            onMouseDown={(event) => {
              // A `tabIndex={-1}` button still takes DOM focus on mousedown;
              // block that so the ring never lands on a dimmed, dead option.
              if (disabled) event.preventDefault()
            }}
            onClick={() => commit(option.value)}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}
