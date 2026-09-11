import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { ApplyButton } from './ApplyButton'

const useSessionMock = vi.fn()
const useMyApplicationMock = vi.fn()
const applyMock = vi.fn()
const registerMock = vi.fn()

vi.mock('../../../entities', () => ({
  useSession: () => useSessionMock(),
  useMyApplication: (jobPostingId: string, enabled: boolean) =>
    useMyApplicationMock(jobPostingId, enabled),
  applicationsClient: { apply: (body: unknown) => applyMock(body) },
  applicationMineQueryKey: (jobPostingId: string) => ['application', 'mine', jobPostingId],
  authClient: { register: (body: unknown) => registerMock(body) },
  sessionQueryKey: ['session', 'me'],
}))

const APPLY_FAILED_MESSAGE = "We couldn't submit your application. Please try again."
const APPLY_SUBMITTED_MESSAGE = 'Your application has been submitted.'

const jobSeekerSession = {
  isPending: false,
  isError: false,
  data: { kind: 'jobSeeker', id: 'js-1', displayName: 'Amara' },
}
const companySession = {
  isPending: false,
  isError: false,
  data: { kind: 'company', id: 'co-1', displayName: 'Cobalt Ledger' },
}

const mineNotApplied = { isPending: false, isError: false, data: { applied: false, appliedAt: undefined } }
const mineApplied = {
  isPending: false,
  isError: false,
  data: { applied: true, appliedAt: '2026-09-10T00:00:00Z' },
}
const minePending = { isPending: true, isError: false, data: undefined }
const mineErrored = { isPending: false, isError: true, data: undefined }

function renderButton() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return render(
    <QueryClientProvider client={queryClient}>
      <ApplyButton jobPostingId="jp-1" />
    </QueryClientProvider>,
  )
}

const signedOutSession = { isPending: false, isError: false, data: null }

beforeEach(() => {
  useSessionMock.mockReturnValue(jobSeekerSession)
  useMyApplicationMock.mockReturnValue(mineNotApplied)
  applyMock.mockReset()
  registerMock.mockReset()
})

describe('ApplyButton', () => {
  it('renders an enabled, type="button" Apply for a signed-in Job Seeker who has not applied', () => {
    renderButton()

    const button = screen.getByRole('button', { name: 'Apply' })
    expect(button).toBeEnabled()
    expect(button).toHaveAttribute('type', 'button')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('submits one application, relabels to a disabled Applied, shows the confirmation, and moves focus there', async () => {
    const user = userEvent.setup()
    applyMock.mockResolvedValue({ id: 'app-1', jobPostingId: 'jp-1', submittedAt: '2026-09-11T00:00:00Z' })
    renderButton()

    await user.click(screen.getByRole('button', { name: 'Apply' }))

    const status = await screen.findByRole('status')
    expect(status).toHaveTextContent(APPLY_SUBMITTED_MESSAGE)
    expect(applyMock).toHaveBeenCalledTimes(1)
    expect(applyMock).toHaveBeenCalledWith({ jobPostingId: 'jp-1' })
    expect(screen.getByRole('button', { name: 'Applied' })).toBeDisabled()
    await waitFor(() => expect(status).toHaveFocus())
  })

  it('does not fire a second application on a double-click; the button is disabled while pending', async () => {
    const user = userEvent.setup()
    applyMock.mockImplementation(() => new Promise(() => {}))
    renderButton()

    const button = screen.getByRole('button', { name: 'Apply' })
    await user.click(button)
    await user.click(button)

    expect(applyMock).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled())
  })

  it('shows the inline failure alert linked via aria-describedby, re-enables Apply, and a retry re-submits', async () => {
    const user = userEvent.setup()
    applyMock
      .mockRejectedValueOnce({ status: 500, title: 'Server error' })
      .mockResolvedValueOnce({ id: 'app-2', jobPostingId: 'jp-1', submittedAt: '2026-09-11T00:00:00Z' })
    renderButton()

    await user.click(screen.getByRole('button', { name: 'Apply' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(APPLY_FAILED_MESSAGE)
    const button = screen.getByRole('button', { name: 'Apply' })
    expect(button).toBeEnabled()
    expect(button).toHaveAttribute('aria-describedby', alert.getAttribute('id'))

    await user.click(button)

    expect(await screen.findByRole('status')).toHaveTextContent(APPLY_SUBMITTED_MESSAGE)
    expect(applyMock).toHaveBeenCalledTimes(2)
  })

  it('renders Applied and disabled on load when the Job Seeker already applied, with no confirmation', () => {
    useMyApplicationMock.mockReturnValue(mineApplied)
    renderButton()

    expect(screen.getByRole('button', { name: 'Applied' })).toBeDisabled()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('renders a disabled Apply while the mine query is pending', () => {
    useMyApplicationMock.mockReturnValue(minePending)
    renderButton()

    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled()
  })

  it('renders an enabled Apply when the mine query errors', () => {
    useMyApplicationMock.mockReturnValue(mineErrored)
    renderButton()

    expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled()
  })

  it('renders nothing for a signed-in Company', () => {
    useSessionMock.mockReturnValue(companySession)
    const { container } = renderButton()

    expect(container).toBeEmptyDOMElement()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('renders nothing while the session is unresolved', () => {
    useSessionMock.mockReturnValue({ isPending: true, isError: false, data: undefined })
    expect(renderButton().container).toBeEmptyDOMElement()
  })
})

describe('ApplyButton — signed-out apply-gate', () => {
  beforeEach(() => {
    useSessionMock.mockReturnValue(signedOutSession)
  })

  it('renders the Apply button (not hidden) for a signed-out visitor, with no modal until clicked', () => {
    renderButton()

    const button = screen.getByRole('button', { name: 'Apply' })
    expect(button).toBeEnabled()
    expect(button).toHaveAttribute('aria-haspopup', 'dialog')
    expect(button).toHaveAttribute('aria-expanded', 'false')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('opens the apply-gate modal on click', async () => {
    const user = userEvent.setup()
    renderButton()

    await user.click(screen.getByRole('button', { name: 'Apply' }))

    expect(screen.getByRole('dialog')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply' })).toHaveAttribute('aria-expanded', 'true')
  })

  it('a successful register closes the gate and fires apply() exactly once', async () => {
    const user = userEvent.setup()
    registerMock.mockResolvedValue({ id: 'js-2', accountType: 'job_seeker', displayName: 'Amara' })
    applyMock.mockResolvedValue({ id: 'app-1', jobPostingId: 'jp-1', submittedAt: '2026-09-11T00:00:00Z' })
    renderButton()

    await user.click(screen.getByRole('button', { name: 'Apply' }))
    await user.type(screen.getByLabelText('Full name'), 'Amara Diallo')
    await user.type(screen.getByLabelText('Email'), 'amara@seeker.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(registerMock).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(applyMock).toHaveBeenCalledTimes(1))
    expect(applyMock).toHaveBeenCalledWith({ jobPostingId: 'jp-1' })
  })

  it('shows the disabled Applied confirmation immediately after a successful register+apply, even before the session refetch resolves', async () => {
    // `useSessionMock` is deliberately left at `signedOutSession` for the
    // whole test — the real app fires an independent `invalidateQueries` for
    // the session that may not have settled yet when the apply POST does.
    // The confirmation must not depend on that race.
    const user = userEvent.setup()
    registerMock.mockResolvedValue({ id: 'js-2', accountType: 'job_seeker', displayName: 'Amara' })
    applyMock.mockResolvedValue({ id: 'app-1', jobPostingId: 'jp-1', submittedAt: '2026-09-11T00:00:00Z' })
    renderButton()

    await user.click(screen.getByRole('button', { name: 'Apply' }))
    await user.type(screen.getByLabelText('Full name'), 'Amara Diallo')
    await user.type(screen.getByLabelText('Email'), 'amara@seeker.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('button', { name: 'Applied' })).toBeDisabled()
    expect(await screen.findByRole('status')).toHaveTextContent(APPLY_SUBMITTED_MESSAGE)
    expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument()
  })

  it('still shows the Applied confirmation if the session refetch triggered by register errors afterward', async () => {
    // The `invalidateQueries` call in `onAuthenticated` is fire-and-forget; if
    // that refetch itself fails, raw `render` would become `'hidden'` (a
    // pending/errored session hides the whole surface) — but the visitor did
    // successfully register and apply, so the confirmation must still show.
    const user = userEvent.setup()
    registerMock.mockResolvedValue({ id: 'js-2', accountType: 'job_seeker', displayName: 'Amara' })
    applyMock.mockResolvedValue({ id: 'app-1', jobPostingId: 'jp-1', submittedAt: '2026-09-11T00:00:00Z' })
    renderButton()

    await user.click(screen.getByRole('button', { name: 'Apply' }))
    await user.type(screen.getByLabelText('Full name'), 'Amara Diallo')
    await user.type(screen.getByLabelText('Email'), 'amara@seeker.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(applyMock).toHaveBeenCalledTimes(1))

    // The session refetch this success triggered comes back errored.
    useSessionMock.mockReturnValue({ isPending: false, isError: true, data: undefined })

    expect(await screen.findByRole('button', { name: 'Applied' })).toBeDisabled()
    expect(await screen.findByRole('status')).toHaveTextContent(APPLY_SUBMITTED_MESSAGE)
  })

  it('does not auto-apply if the gate is closed (Escape/scrim) before an in-flight register resolves', async () => {
    const user = userEvent.setup()
    let resolveRegister: (value: unknown) => void = () => {}
    registerMock.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveRegister = resolve
        }),
    )
    renderButton()

    await user.click(screen.getByRole('button', { name: 'Apply' }))
    await user.type(screen.getByLabelText('Full name'), 'Amara Diallo')
    await user.type(screen.getByLabelText('Email'), 'amara@seeker.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    // Cancel while register is still in flight.
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()

    // The register request the (now-unmounted) modal fired still resolves.
    await act(async () => {
      resolveRegister({ id: 'js-3', accountType: 'job_seeker', displayName: 'Amara' })
    })

    expect(applyMock).not.toHaveBeenCalled()
    expect(screen.queryByRole('button', { name: 'Applied' })).not.toBeInTheDocument()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    // The gate trigger is back, ready to be reopened.
    expect(screen.getByRole('button', { name: 'Apply' })).toBeEnabled()
  })

  it('shows the apply-failure alert on the (now-visible, signed-in) button after a successful register, not inside the closed modal', async () => {
    const user = userEvent.setup()
    registerMock.mockResolvedValue({ id: 'js-2', accountType: 'job_seeker', displayName: 'Amara' })
    let rejectApply: (reason: unknown) => void = () => {}
    applyMock.mockImplementation(
      () =>
        new Promise((_resolve, reject) => {
          rejectApply = reject
        }),
    )
    renderButton()

    await user.click(screen.getByRole('button', { name: 'Apply' }))
    await user.type(screen.getByLabelText('Full name'), 'Amara Diallo')
    await user.type(screen.getByLabelText('Email'), 'amara@seeker.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    await waitFor(() => expect(applyMock).toHaveBeenCalledTimes(1))

    // Session resolves to signed-in Job Seeker once the gate closes, matching
    // the real post-invalidation flow — set this before the apply request
    // settles so the failure renders on the (now-visible) button branch.
    useSessionMock.mockReturnValue(jobSeekerSession)
    await act(async () => {
      rejectApply({ status: 500, title: 'Server error' })
    })

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(APPLY_FAILED_MESSAGE)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
