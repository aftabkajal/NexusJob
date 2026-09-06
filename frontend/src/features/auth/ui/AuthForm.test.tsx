import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { authClient } from '../../../entities'

import { AuthForm } from './AuthForm'

const mockNavigate = vi.fn()

vi.mock('react-router', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router')>()
  return { ...actual, useNavigate: () => mockNavigate }
})

vi.mock('../../../entities', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../entities')>()
  return {
    ...actual,
    authClient: { register: vi.fn(), login: vi.fn(), logout: vi.fn(), me: vi.fn() },
  }
})

const register = vi.mocked(authClient.register)
const login = vi.mocked(authClient.login)

function renderForm() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries')
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthForm />
      </MemoryRouter>
    </QueryClientProvider>,
  )
  return { invalidateSpy }
}

beforeEach(() => {
  mockNavigate.mockReset()
  register.mockReset()
  login.mockReset()
})

describe('AuthForm', () => {
  it('renders sign-up mode by default with a required Company name field and a disabled Job Seeker option', () => {
    renderForm()

    expect(screen.getByRole('heading', { name: 'Create your account' })).toBeInTheDocument()
    expect(screen.getByLabelText('Company name')).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Company' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Job Seeker' })).toHaveAttribute('aria-disabled', 'true')
    expect(screen.getByText('Job Seeker accounts are coming soon.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create account' })).toHaveAttribute('type', 'submit')
  })

  it('switches to log-in mode: name field removed, labels change, email and password retained', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.type(screen.getByLabelText('Email'), 'raj@acme.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Log in' }))

    expect(screen.getByRole('heading', { name: 'Log in' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Company name')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('raj@acme.test')
    expect(screen.getByLabelText('Password')).toHaveValue('password123')
    expect(screen.getByRole('button', { name: 'Log in' })).toHaveAttribute('type', 'submit')
  })

  it('validates on blur, never per keystroke', async () => {
    const user = userEvent.setup()
    renderForm()

    const email = screen.getByLabelText('Email')
    await user.type(email, 'not-an-email')
    expect(screen.queryByText('Enter a valid email address.')).not.toBeInTheDocument()

    await user.tab()
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
  })

  it('blocks submit while any field is invalid and sends no request', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(screen.getByText('Enter your company name.')).toBeInTheDocument()
    expect(screen.getByText('Enter your email address.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
    expect(register).not.toHaveBeenCalled()
  })

  it('associates a field error with its input via aria-describedby and aria-invalid', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(screen.getByRole('button', { name: 'Create account' }))

    const email = screen.getByLabelText('Email')
    const describedBy = email.getAttribute('aria-describedby')
    expect(describedBy).toBeTruthy()
    expect(document.getElementById(describedBy as string)).toHaveTextContent(
      'Enter your email address.',
    )
    expect(email).toHaveAttribute('aria-invalid', 'true')
  })

  it('registers a Company with accountType "company", invalidates the session, and navigates to "/"', async () => {
    const user = userEvent.setup()
    register.mockResolvedValue({ id: 'c-1', accountType: 'company', displayName: 'Acme Inc' })
    const { invalidateSpy } = renderForm()

    await user.type(screen.getByLabelText('Company name'), 'Acme Inc')
    await user.type(screen.getByLabelText('Email'), 'hiring@acme.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/'))
    expect(register).toHaveBeenCalledWith({
      accountType: 'company',
      name: 'Acme Inc',
      email: 'hiring@acme.test',
      password: 'password123',
    })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['session', 'me'] })
  })

  it('shows the duplicate-email message under the email field on a 409, keeping name and password', async () => {
    const user = userEvent.setup()
    register.mockRejectedValue({ status: 409, title: 'Conflict' })
    renderForm()

    await user.type(screen.getByLabelText('Company name'), 'Acme Inc')
    await user.type(screen.getByLabelText('Email'), 'taken@acme.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() =>
      expect(
        screen.getByText('This email is already registered as a Company.'),
      ).toBeInTheDocument(),
    )
    const email = screen.getByLabelText('Email')
    const describedBy = email.getAttribute('aria-describedby')
    expect(document.getElementById(describedBy as string)).toHaveTextContent(
      'This email is already registered as a Company.',
    )
    expect(screen.getByLabelText('Company name')).toHaveValue('Acme Inc')
    expect(screen.getByLabelText('Password')).toHaveValue('password123')
    expect(mockNavigate).not.toHaveBeenCalled()
  })

  it('logs in an existing Company, invalidates the session, and navigates to "/"', async () => {
    const user = userEvent.setup()
    login.mockResolvedValue({ id: 'c-9', accountType: 'company', displayName: 'Cobalt Ledger' })
    const { invalidateSpy } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Log in' })) // sign-up -> log-in
    await user.type(screen.getByLabelText('Email'), 'hiring@cobalt.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Log in' })) // submit

    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/'))
    expect(login).toHaveBeenCalledWith({
      accountType: 'company',
      email: 'hiring@cobalt.test',
      password: 'password123',
    })
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['session', 'me'] })
    expect(register).not.toHaveBeenCalled()
  })

  it('shows the generic mismatch message as a form-level alert on a 401 sign-in', async () => {
    const user = userEvent.setup()
    login.mockRejectedValue({ status: 401, title: 'Unauthorized' })
    renderForm()

    await user.click(screen.getByRole('button', { name: 'Log in' })) // sign-up -> log-in
    await user.type(screen.getByLabelText('Email'), 'raj@acme.test')
    await user.type(screen.getByLabelText('Password'), 'wrongpass1')
    await user.click(screen.getByRole('button', { name: 'Log in' })) // submit

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent("That email and password don't match. Please try again.")
    expect(screen.getByLabelText('Email')).not.toHaveAttribute('aria-invalid')
    expect(login).toHaveBeenCalledWith({
      accountType: 'company',
      email: 'raj@acme.test',
      password: 'wrongpass1',
    })
  })

  it('maps a server 400 errors entry onto the matching field', async () => {
    const user = userEvent.setup()
    register.mockRejectedValue({
      status: 400,
      errors: { Email: ['That email address is not permitted.'] },
    })
    renderForm()

    await user.type(screen.getByLabelText('Company name'), 'Acme Inc')
    await user.type(screen.getByLabelText('Email'), 'blocked@acme.test')
    await user.type(screen.getByLabelText('Password'), 'password123')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    const email = screen.getByLabelText('Email')
    await waitFor(() => {
      const describedBy = email.getAttribute('aria-describedby')
      expect(describedBy).toBeTruthy()
      expect(document.getElementById(describedBy as string)).toHaveTextContent(
        'That email address is not permitted.',
      )
    })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(mockNavigate).not.toHaveBeenCalled()
  })
})
