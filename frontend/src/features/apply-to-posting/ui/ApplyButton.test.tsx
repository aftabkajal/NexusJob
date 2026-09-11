import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { ApplyButton } from './ApplyButton'

const useSessionMock = vi.fn()
const useMyApplicationMock = vi.fn()
const applyMock = vi.fn()

vi.mock('../../../entities', () => ({
  useSession: () => useSessionMock(),
  useMyApplication: (jobPostingId: string, enabled: boolean) =>
    useMyApplicationMock(jobPostingId, enabled),
  applicationsClient: { apply: (body: unknown) => applyMock(body) },
  applicationMineQueryKey: (jobPostingId: string) => ['application', 'mine', jobPostingId],
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

beforeEach(() => {
  useSessionMock.mockReturnValue(jobSeekerSession)
  useMyApplicationMock.mockReturnValue(mineNotApplied)
  applyMock.mockReset()
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

  it('renders nothing for a signed-out visitor', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: false, data: null })
    expect(renderButton().container).toBeEmptyDOMElement()
  })

  it('renders nothing while the session is unresolved', () => {
    useSessionMock.mockReturnValue({ isPending: true, isError: false, data: undefined })
    expect(renderButton().container).toBeEmptyDOMElement()
  })
})
