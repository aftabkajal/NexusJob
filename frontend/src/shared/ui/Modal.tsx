import { useEffect, useRef, type KeyboardEvent, type ReactNode, type RefObject } from 'react'

import { createPortal } from 'react-dom'

import styles from './Modal.module.css'

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  // The selector's `button:not([disabled])` (etc.) clauses match a control
  // like `RoleToggle`'s disabled option, which is excluded from native Tab
  // flow via `tabIndex={-1}` rather than the `disabled` attribute (it must
  // stay clickable-looking but non-interactive). Filter on the live
  // `.tabIndex` property — not just the `[tabindex="-1"]` attribute clause —
  // so any such element is dropped from the trap's candidate list regardless
  // of which selector clause matched it.
  return Array.from(container.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)).filter(
    (element) => element.tabIndex !== -1,
  )
}

export interface ModalProps {
  /** id of the element that labels the dialog (`aria-labelledby`). */
  titleId: string
  /** Called on `Escape`, a scrim click, or any other request to dismiss. */
  onClose: () => void
  /** Element to move focus to on open. Defaults to the first focusable
   * element found inside the panel. */
  initialFocusRef?: RefObject<HTMLElement | null>
  children: ReactNode
}

/**
 * `shared/ui/Modal` — the app's first dialog primitive (Story 3.2). Presentational
 * only: no domain knowledge, portal-rendered, focus-trapped, Escape/scrim-close.
 * Hand-rolled per the Boundaries decision (no dialog/focus-trap library in the
 * project) — mirrors `RoleToggle`'s hand-rolled roving-tabindex precedent.
 *
 * On mount: captures `document.activeElement` and moves focus to
 * `initialFocusRef?.current`, falling back to the first focusable element inside
 * the panel. On unmount: restores focus to the captured element. A `keydown`
 * handler on the panel closes on `Escape` and wraps `Tab` / `Shift+Tab` at the
 * first/last focusable control so focus never escapes to the dimmed page behind
 * it.
 */
export function Modal({ titleId, onClose, initialFocusRef, children }: ModalProps) {
  const panelRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null

    const panel = panelRef.current
    // Falls back to the panel itself (given `tabIndex={-1}` below) when a
    // consumer passes no `initialFocusRef` and renders no focusable content —
    // an edge case no current consumer hits, but focus must land somewhere
    // inside the dialog rather than staying stranded on the trigger behind
    // the dimmed overlay.
    const toFocus = initialFocusRef?.current ?? (panel ? getFocusableElements(panel)[0] : undefined) ?? panel
    toFocus?.focus()

    return () => {
      // `.isConnected` guards a `previouslyFocused` element that was removed
      // from the DOM while the modal was open — `.focus()` on a detached
      // element is a silent no-op, so this only matters for correctness, not
      // for crash-safety, but it keeps the intent explicit.
      if (previouslyFocused?.isConnected) {
        previouslyFocused.focus()
      }
    }
    // Intentionally run once, on mount/unmount only — re-running on every
    // render would re-steal focus from whatever the visitor just interacted
    // with. `initialFocusRef`/`onClose` are read once on open, not tracked.
  }, [])

  useEffect(() => {
    // A `document`-level listener, not a panel-scoped `onKeyDown`: Escape must
    // close the modal no matter where focus currently is. A control that
    // disables mid-interaction (e.g. the submit button while its request is
    // in flight) drops focus to `document.body`, outside the panel's own DOM
    // subtree — an `onKeyDown` on the panel never sees a key pressed there.
    function handleEscape(event: globalThis.KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.addEventListener('keydown', handleEscape)
    return () => document.removeEventListener('keydown', handleEscape)
    // Intentionally run once, on mount/unmount only, mirroring the focus
    // effect above — `onClose` is read once, not tracked.
  }, [])

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key !== 'Tab') return

    const panel = panelRef.current
    if (!panel) return
    const focusable = getFocusableElements(panel)
    if (focusable.length === 0) return

    const first = focusable[0]
    const last = focusable[focusable.length - 1]

    if (event.shiftKey) {
      if (document.activeElement === first) {
        event.preventDefault()
        last.focus()
      }
      return
    }

    if (document.activeElement === last) {
      event.preventDefault()
      first.focus()
    }
  }

  return createPortal(
    <div className={styles.overlay} onClick={onClose}>
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        // Programmatically focusable (never a Tab stop of its own — excluded
        // from `getFocusableElements` the same way `RoleToggle`'s disabled
        // option is) so the initial-focus fallback above always has a target
        // inside the dialog, even with no focusable children.
        tabIndex={-1}
        className={styles.panel}
        onClick={(event) => event.stopPropagation()}
        onKeyDown={handleKeyDown}
      >
        {children}
      </div>
    </div>,
    document.body,
  )
}
