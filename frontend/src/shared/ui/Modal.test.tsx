import { useId, useState } from 'react'

import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { Modal } from './Modal'

/** A trigger + modal harness so open/close and focus-restore can be exercised
 * the way a real consumer (`ApplyGateModal`) would use it. */
function Harness({ onCloseSpy }: { onCloseSpy: () => void }) {
  const [open, setOpen] = useState(false)
  const titleId = useId()

  return (
    <div>
      <button type="button" onClick={() => setOpen(true)}>
        Open
      </button>
      {open && (
        <Modal
          titleId={titleId}
          onClose={() => {
            onCloseSpy()
            setOpen(false)
          }}
        >
          <h2 id={titleId}>Dialog title</h2>
          <button type="button">First</button>
          <button type="button">Second</button>
          <button type="button">Last</button>
        </Modal>
      )}
    </div>
  )
}

describe('Modal', () => {
  it('renders via a portal into document.body with dialog semantics', () => {
    render(<Modal titleId="t1" onClose={vi.fn()}><h2 id="t1">Title</h2></Modal>)

    const dialog = screen.getByRole('dialog')
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toHaveAttribute('aria-labelledby', 't1')
    expect(dialog.closest('body')).toBe(document.body)
  })

  it('moves initial focus to initialFocusRef when given', () => {
    function WithRef() {
      const ref = useRefButton()
      return (
        <Modal titleId="t2" onClose={vi.fn()} initialFocusRef={ref.ref}>
          <h2 id="t2">Title</h2>
          <button type="button">Not first</button>
          <button type="button" ref={ref.setRef}>
            Target
          </button>
        </Modal>
      )
    }
    render(<WithRef />)

    expect(screen.getByRole('button', { name: 'Target' })).toHaveFocus()
  })

  it('moves initial focus to the first focusable child when initialFocusRef is omitted', () => {
    render(
      <Modal titleId="t3" onClose={vi.fn()}>
        <h2 id="t3">Title</h2>
        <button type="button">First</button>
        <button type="button">Second</button>
      </Modal>,
    )

    expect(screen.getByRole('button', { name: 'First' })).toHaveFocus()
  })

  it('falls back to focusing the dialog panel itself with no initialFocusRef and no focusable content', () => {
    render(
      <Modal titleId="t3b" onClose={vi.fn()}>
        <h2 id="t3b">Title</h2>
        <p>Nothing focusable here.</p>
      </Modal>,
    )

    expect(screen.getByRole('dialog')).toHaveFocus()
  })

  it('calls onClose on Escape', async () => {
    const user = userEvent.setup()
    const onClose = vi.fn()
    render(
      <Modal titleId="t4" onClose={onClose}>
        <h2 id="t4">Title</h2>
        <button type="button">First</button>
      </Modal>,
    )

    await user.keyboard('{Escape}')

    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('calls onClose on Escape even when focus has moved outside the panel (e.g. a control disabled mid-interaction)', async () => {
    // The Escape listener must not depend on the keypress bubbling up through
    // the panel: a button that disables while it holds focus (a pending
    // mutation, say) drops focus to `document.body`, outside the panel's own
    // DOM subtree.
    const user = userEvent.setup()
    const onClose = vi.fn()
    render(
      <Modal titleId="t4b" onClose={onClose}>
        <h2 id="t4b">Title</h2>
        <button type="button">First</button>
      </Modal>,
    )

    ;(document.activeElement as HTMLElement | null)?.blur()
    document.body.focus()
    expect(document.activeElement).toBe(document.body)

    await user.keyboard('{Escape}')

    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('calls onClose on an overlay click but not on a click inside the panel', async () => {
    const user = userEvent.setup()
    const onClose = vi.fn()
    render(
      <Modal titleId="t5" onClose={onClose}>
        <h2 id="t5">Title</h2>
        <button type="button">First</button>
      </Modal>,
    )

    await user.click(screen.getByRole('button', { name: 'First' }))
    expect(onClose).not.toHaveBeenCalled()

    await user.click(screen.getByRole('dialog').parentElement as HTMLElement)
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  it('wraps Tab from the last focusable control to the first, and Shift+Tab from the first to the last', async () => {
    const user = userEvent.setup()
    render(
      <Modal titleId="t6" onClose={vi.fn()}>
        <h2 id="t6">Title</h2>
        <button type="button">First</button>
        <button type="button">Second</button>
        <button type="button">Last</button>
      </Modal>,
    )

    const first = screen.getByRole('button', { name: 'First' })
    const last = screen.getByRole('button', { name: 'Last' })

    last.focus()
    await user.tab()
    expect(first).toHaveFocus()

    await user.tab({ shift: true })
    expect(last).toHaveFocus()
  })

  it('excludes a tabIndex={-1} control from the trap even when it matches the selector some other way', async () => {
    // Mirrors `RoleToggle`'s disabled option: a plain enabled `<button>` (no
    // `disabled` attribute, so it matches `button:not([disabled])`) taken out
    // of native Tab flow via `tabIndex={-1}` alone. The trap must not wrap
    // Tab/Shift+Tab onto it, or it becomes a keyboard dead end inside the
    // modal — and, more seriously, the wrap comparison silently stops
    // matching and native Tab handling can carry focus out of the trap
    // entirely on the very next Shift+Tab.
    const user = userEvent.setup()
    render(
      <Modal titleId="t7" onClose={vi.fn()}>
        <h2 id="t7">Title</h2>
        <button type="button" tabIndex={-1}>
          Skipped
        </button>
        <button type="button">Reachable first</button>
        <button type="button">Reachable last</button>
      </Modal>,
    )

    const reachableFirst = screen.getByRole('button', { name: 'Reachable first' })
    const reachableLast = screen.getByRole('button', { name: 'Reachable last' })

    reachableLast.focus()
    await user.tab()
    expect(reachableFirst).toHaveFocus()

    await user.tab({ shift: true })
    expect(reachableLast).toHaveFocus()
  })

  it('restores focus to the element that was focused before the modal opened', async () => {
    const user = userEvent.setup()
    const onCloseSpy = vi.fn()
    render(<Harness onCloseSpy={onCloseSpy} />)

    const openButton = screen.getByRole('button', { name: 'Open' })
    openButton.focus()
    await user.click(openButton)

    expect(screen.getByRole('button', { name: 'First' })).toHaveFocus()

    await user.keyboard('{Escape}')

    expect(onCloseSpy).toHaveBeenCalledTimes(1)
    expect(openButton).toHaveFocus()
  })
})

// A tiny helper so `initialFocusRef` can point at a specific rendered button
// without pulling in an extra test-only component file.
function useRefButton() {
  const ref = { current: null as HTMLButtonElement | null }
  const setRef = (el: HTMLButtonElement | null) => {
    ref.current = el
  }
  return { ref, setRef }
}
