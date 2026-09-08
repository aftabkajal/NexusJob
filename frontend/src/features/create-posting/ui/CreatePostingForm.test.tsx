import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { jobPostingsClient } from '../../../entities'

import { CreatePostingForm } from './CreatePostingForm'

vi.mock('../../../entities', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../entities')>()
  return {
    ...actual,
    jobPostingsClient: { create: vi.fn() },
  }
})

const create = vi.mocked(jobPostingsClient.create)

function renderForm() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const router = createMemoryRouter(
    [
      { path: '/post-a-job', element: <CreatePostingForm /> },
      { path: '/job-postings/:id', element: <div>posting-detail</div> },
    ],
    { initialEntries: ['/post-a-job'] },
  )
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return router
}

beforeEach(() => {
  create.mockReset()
})

describe('CreatePostingForm', () => {
  it('renders a title input, a description textarea, and a single Publish submit', () => {
    renderForm()

    expect(screen.getByRole('heading', { name: 'Post a job' })).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toBeInTheDocument()
    expect(screen.getByLabelText('Description').tagName).toBe('TEXTAREA')
    const publish = screen.getByRole('button', { name: 'Publish' })
    expect(publish).toHaveAttribute('type', 'submit')
    expect(screen.getAllByRole('button')).toHaveLength(1)
  })

  it('caps the controls natively at the server length limits', () => {
    renderForm()

    expect(screen.getByLabelText('Title')).toHaveAttribute('maxLength', '200')
    expect(screen.getByLabelText('Description')).toHaveAttribute('maxLength', '4000')
  })

  it('validates on blur, never per keystroke', async () => {
    const user = userEvent.setup()
    renderForm()

    const title = screen.getByLabelText('Title')
    await user.type(title, '   ')
    expect(screen.queryByText('Enter a title for this posting.')).not.toBeInTheDocument()

    await user.tab()
    expect(screen.getByText('Enter a title for this posting.')).toBeInTheDocument()
    expect(create).not.toHaveBeenCalled()
  })

  it('blocks submit while a field is empty, shows inline aria-linked errors, and sends no request', async () => {
    const user = userEvent.setup()
    renderForm()

    await user.click(screen.getByRole('button', { name: 'Publish' }))

    expect(screen.getByText('Enter a title for this posting.')).toBeInTheDocument()
    expect(screen.getByText('Enter a description for this posting.')).toBeInTheDocument()

    const title = screen.getByLabelText('Title')
    const describedBy = title.getAttribute('aria-describedby')
    expect(describedBy).toBeTruthy()
    expect(document.getElementById(describedBy as string)).toHaveTextContent(
      'Enter a title for this posting.',
    )
    expect(title).toHaveAttribute('aria-invalid', 'true')
    expect(create).not.toHaveBeenCalled()
  })

  // `fireEvent.change` bypasses the native `maxLength` cap so the belt-and-braces
  // client length check (which still backstops autofill / programmatic input) runs.
  it('blocks submit when the title exceeds 200 characters', async () => {
    const user = userEvent.setup()
    renderForm()

    fireEvent.change(screen.getByLabelText('Description'), {
      target: { value: 'A valid description.' },
    })
    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'a'.repeat(201) } })
    await user.click(screen.getByRole('button', { name: 'Publish' }))

    expect(screen.getByText('The title must be 200 characters or fewer.')).toBeInTheDocument()
    expect(create).not.toHaveBeenCalled()
  })

  it('blocks submit when the description exceeds 4000 characters', async () => {
    const user = userEvent.setup()
    renderForm()

    fireEvent.change(screen.getByLabelText('Title'), { target: { value: 'A valid title' } })
    fireEvent.change(screen.getByLabelText('Description'), {
      target: { value: 'a'.repeat(4001) },
    })
    await user.click(screen.getByRole('button', { name: 'Publish' }))

    expect(
      screen.getByText('The description must be 4000 characters or fewer.'),
    ).toBeInTheDocument()
    expect(create).not.toHaveBeenCalled()
  })

  it('calls create with trimmed values and navigates to the new posting on success', async () => {
    const user = userEvent.setup()
    create.mockResolvedValue({
      id: 'new-id',
      title: 'Staff Engineer',
      description: 'Build things.',
      createdAt: '2026-09-08T00:00:00Z',
    })
    const router = renderForm()

    await user.type(screen.getByLabelText('Title'), '  Staff Engineer  ')
    await user.type(screen.getByLabelText('Description'), '  Build things.  ')
    await user.click(screen.getByRole('button', { name: 'Publish' }))

    await waitFor(() =>
      expect(router.state.location.pathname).toBe('/job-postings/new-id'),
    )
    expect(create).toHaveBeenCalledWith({ title: 'Staff Engineer', description: 'Build things.' })
    expect(screen.getByText('posting-detail')).toBeInTheDocument()
  })

  it('does not fire a second create while the first request is in flight', async () => {
    const user = userEvent.setup()
    create.mockReturnValue(new Promise(() => {}))
    renderForm()

    await user.type(screen.getByLabelText('Title'), 'PM')
    await user.type(screen.getByLabelText('Description'), 'Roadmap.')
    const publish = screen.getByRole('button', { name: 'Publish' })
    await user.click(publish)
    await user.click(publish)

    expect(create).toHaveBeenCalledTimes(1)
  })

  it('shows the failure banner with values retained on a network error, and a retry re-submits the same values', async () => {
    const user = userEvent.setup()
    create.mockRejectedValueOnce({ message: 'network down' })
    create.mockResolvedValueOnce({
      id: 'jp-9',
      title: 'PM',
      description: 'Roadmap.',
      createdAt: '2026-09-08T00:00:00Z',
    })
    const router = renderForm()

    await user.type(screen.getByLabelText('Title'), 'PM')
    await user.type(screen.getByLabelText('Description'), 'Roadmap.')
    await user.click(screen.getByRole('button', { name: 'Publish' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "We couldn't publish this posting. Please try again.",
    )
    expect(screen.getByLabelText('Title')).toHaveValue('PM')
    expect(screen.getByLabelText('Description')).toHaveValue('Roadmap.')

    await user.click(screen.getByRole('button', { name: 'Publish' }))

    await waitFor(() => expect(create).toHaveBeenCalledTimes(2))
    expect(create).toHaveBeenNthCalledWith(2, { title: 'PM', description: 'Roadmap.' })
    await waitFor(() => expect(router.state.location.pathname).toBe('/job-postings/jp-9'))
  })

  it('renders a server 400 field error under the Title field with no banner', async () => {
    const user = userEvent.setup()
    create.mockRejectedValue({
      status: 400,
      errors: { Title: ['That title is not permitted.'] },
    })
    renderForm()

    await user.type(screen.getByLabelText('Title'), 'Spammy title')
    await user.type(screen.getByLabelText('Description'), 'A valid description.')
    await user.click(screen.getByRole('button', { name: 'Publish' }))

    const title = screen.getByLabelText('Title')
    await waitFor(() => {
      const describedBy = title.getAttribute('aria-describedby')
      expect(describedBy).toBeTruthy()
      expect(document.getElementById(describedBy as string)).toHaveTextContent(
        'That title is not permitted.',
      )
    })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('Spammy title')
    expect(screen.getByLabelText('Description')).toHaveValue('A valid description.')
  })

  it('renders a server 400 field error under the Description field with no banner', async () => {
    const user = userEvent.setup()
    create.mockRejectedValue({
      status: 400,
      errors: { Description: ['That description is not permitted.'] },
    })
    renderForm()

    await user.type(screen.getByLabelText('Title'), 'A valid title')
    await user.type(screen.getByLabelText('Description'), 'Spammy description')
    await user.click(screen.getByRole('button', { name: 'Publish' }))

    const description = screen.getByLabelText('Description')
    await waitFor(() => {
      const describedBy = description.getAttribute('aria-describedby')
      expect(describedBy).toBeTruthy()
      expect(document.getElementById(describedBy as string)).toHaveTextContent(
        'That description is not permitted.',
      )
    })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('A valid title')
    expect(screen.getByLabelText('Description')).toHaveValue('Spammy description')
  })
})
