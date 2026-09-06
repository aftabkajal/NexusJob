import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it } from 'vitest'

import { routes } from './App'

/**
 * Mounts the real route tree (from `App.tsx`) with an in-memory router, covering
 * the I/O-matrix rows "Anonymous visitor opens `/`" and "Unknown client route".
 */
function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  return render(<RouterProvider router={router} />)
}

describe('App routing — anonymous viewer', () => {
  it('renders the shell and the Home landing surface at "/"', () => {
    renderAt('/')

    // Shell chrome
    expect(screen.getByRole('link', { name: 'NexusJob' })).toHaveAttribute('href', '/')
    const nav = screen.getByRole('navigation', { name: 'Primary' })
    expect(nav).toBeInTheDocument()

    // Home surface copy
    expect(
      screen.getByRole('heading', { name: 'Find your next role. Post your next hire.' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('search')).toBeInTheDocument()
    expect(
      screen.getByText('Browsing and searching do not require an account.'),
    ).toBeInTheDocument()
    expect(screen.getByText('No open postings yet. Check back soon.')).toBeInTheDocument()
  })

  it('renders the not-available surface inside the shell for an unknown path', () => {
    renderAt('/does-not-exist')

    // Still inside the shell
    expect(screen.getByRole('navigation', { name: 'Primary' })).toBeInTheDocument()

    // Not-found surface
    expect(
      screen.getByRole('heading', { name: 'This page is not available.' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Return to the home page.' })).toHaveAttribute(
      'href',
      '/',
    )
  })
})
