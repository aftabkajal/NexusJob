import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it, vi } from 'vitest'

import { PostAJobPage } from './PostAJobPage'

const useSessionMock = vi.fn()

vi.mock('../../entities', () => ({
  useSession: () => useSessionMock(),
}))

vi.mock('../../features', () => ({
  CreatePostingForm: () => <div>create-posting-form</div>,
}))

function renderPage() {
  const router = createMemoryRouter(
    [
      { path: '/post-a-job', element: <PostAJobPage /> },
      { path: '/', element: <div>home-landing</div> },
    ],
    { initialEntries: ['/post-a-job'] },
  )
  render(<RouterProvider router={router} />)
  return router
}

describe('PostAJobPage guard', () => {
  it('renders nothing and does not redirect while the session is still pending', () => {
    useSessionMock.mockReturnValue({ isPending: true, isError: false, data: undefined })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/post-a-job')
    expect(screen.queryByText('home-landing')).not.toBeInTheDocument()
    expect(screen.queryByText('create-posting-form')).not.toBeInTheDocument()
  })

  it('renders nothing and does not redirect when the session query has errored', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: true, data: undefined })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/post-a-job')
    expect(screen.queryByText('create-posting-form')).not.toBeInTheDocument()
  })

  it('renders the form once the session resolves to a Company', () => {
    useSessionMock.mockReturnValue({
      isPending: false,
      isError: false,
      data: { kind: 'company', id: 'c-1', displayName: 'Cobalt Ledger' },
    })
    renderPage()

    expect(screen.getByText('create-posting-form')).toBeInTheDocument()
  })

  it('redirects to "/" once the session resolves to a non-Company', () => {
    useSessionMock.mockReturnValue({ isPending: false, isError: false, data: null })
    const router = renderPage()

    expect(router.state.location.pathname).toBe('/')
    expect(screen.getByText('home-landing')).toBeInTheDocument()
  })
})
