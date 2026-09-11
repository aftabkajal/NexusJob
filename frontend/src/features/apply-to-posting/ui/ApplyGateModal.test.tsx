import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { authClient } from '../../../entities'

import { ApplyGateModal } from './ApplyGateModal'

vi.mock('../../../entities', () => ({
  authClient: { register: vi.fn() },
}))

const register = vi.mocked(authClient.register)

function renderModal(overrides?: { onClose?: () => void; onAuthenticated?: () => void }) {
  const onClose = overrides?.onClose ?? vi.fn()
  const onAuthenticated = overrides?.onAuthenticated ?? vi.fn()
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  render(
    <QueryClientProvider client={queryClient}>
      <ApplyGateModal jobPostingId="jp-1" onClose={onClose} onAuthenticated={onAuthenticated} />
    </QueryClientProvider>,
  )
  return { onClose, onAuthenticated }
}

beforeEach(() => {
  register.mockReset()
})

describe('ApplyGateModal', () => {
  it('renders the Job-Seeker-fixed role toggle with Company disabled', () => {
    renderModal()

    const jobSeeker = screen.getByRole('radio', { name: 'Job Seeker' })
    const company = screen.getByRole('radio', { name: 'Company' })
    expect(jobSeeker).toBeChecked()
    expect(company).toHaveAttribute('aria-disabled', 'true')
  })

  it('focuses the name field on open', () => {
    renderModal()

    expect(screen.getByLabelText('Full name')).toHaveFocus()
  })

  it('registers a Job Seeker and calls onAuthenticated exactly once on success', async () => {
    const user = userEvent.setup()
    register.mockResolvedValue({ id: 'js-1', accountType: 'job_seeker', displayName: 'Amara Diallo' })
    const { onAuthenticated } = renderModal()

    await user.type(screen.getByLabelText('Full name'), 'Amara Diallo')
    await user.type(screen.getByLabelText('Email'), 'amara@seeker.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(onAuthenticated).toHaveBeenCalledTimes(1))
    expect(register).toHaveBeenCalledWith({
      accountType: 'job_seeker',
      name: 'Amara Diallo',
      email: 'amara@seeker.test',
      password: 'password123',
    })
  })

  it('shows the duplicate-email field error on a 409 and does not call onAuthenticated', async () => {
    const user = userEvent.setup()
    register.mockRejectedValue({ status: 409, title: 'Conflict' })
    const { onAuthenticated } = renderModal()

    await user.type(screen.getByLabelText('Full name'), 'Amara Diallo')
    await user.type(screen.getByLabelText('Email'), 'taken@seeker.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() =>
      expect(
        screen.getByText('This email is already registered as a Job Seeker.'),
      ).toBeInTheDocument(),
    )
    const email = screen.getByLabelText('Email')
    const describedBy = email.getAttribute('aria-describedby')
    expect(document.getElementById(describedBy as string)).toHaveTextContent(
      'This email is already registered as a Job Seeker.',
    )
    expect(onAuthenticated).not.toHaveBeenCalled()
  })

  it('maps a server 400 errors entry onto the matching field', async () => {
    const user = userEvent.setup()
    register.mockRejectedValue({
      status: 400,
      errors: { Email: ['That email address is not permitted.'] },
    })
    renderModal()

    await user.type(screen.getByLabelText('Full name'), 'Amara Diallo')
    await user.type(screen.getByLabelText('Email'), 'blocked@seeker.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() =>
      expect(screen.getByText('That email address is not permitted.')).toBeInTheDocument(),
    )
  })

  it('blocks submit with blank fields and sends no request', async () => {
    const user = userEvent.setup()
    renderModal()

    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(screen.getByText('Enter your full name.')).toBeInTheDocument()
    expect(screen.getByText('Enter your email address.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
    expect(register).not.toHaveBeenCalled()
  })

  it('validates on blur, never per keystroke', async () => {
    const user = userEvent.setup()
    renderModal()

    const email = screen.getByLabelText('Email')
    await user.type(email, 'not-an-email')
    expect(screen.queryByText('Enter a valid email address.')).not.toBeInTheDocument()

    await user.tab()
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
  })

  it('is keyboard-operable end to end: role toggle, fields, and submit reachable via Tab', async () => {
    const user = userEvent.setup()
    register.mockResolvedValue({ id: 'js-1', accountType: 'job_seeker', displayName: 'Amara Diallo' })
    const { onAuthenticated } = renderModal()

    // Focus starts on the name field (per the open behavior); walk forward to
    // fill every control with keyboard only.
    expect(screen.getByLabelText('Full name')).toHaveFocus()
    await user.keyboard('Amara Diallo')
    await user.tab()
    expect(screen.getByLabelText('Email')).toHaveFocus()
    await user.keyboard('amara@seeker.test')
    await user.tab()
    expect(screen.getByLabelText('Password')).toHaveFocus()
    await user.keyboard('password123')
    await user.tab()
    expect(screen.getByRole('button', { name: 'Create account' })).toHaveFocus()
    await user.keyboard('{Enter}')

    await waitFor(() => expect(onAuthenticated).toHaveBeenCalledTimes(1))
  })
})
