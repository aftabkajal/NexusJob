import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'

import { NavBar, navItemsFor } from './NavBar'

/** Surfaces only a signed-in Company or Job Seeker may reach. */
const ROLE_RESTRICTED_ROUTES = ['/post-a-job', '/my-postings', '/my-applications']

function renderNavBar() {
  return render(
    <MemoryRouter>
      <NavBar viewer={{ kind: 'anonymous' }} />
    </MemoryRouter>,
  )
}

describe('NavBar — anonymous viewer', () => {
  it('renders exactly the Search and Sign up / Log in nav links', () => {
    renderNavBar()

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    const labels = within(nav)
      .getAllByRole('link')
      .map((link) => link.textContent)

    expect(labels).toEqual(['Search', 'Sign up / Log in'])
  })

  it('points no nav link at a Company-only or Job-Seeker-only surface', () => {
    renderNavBar()

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    for (const link of within(nav).getAllByRole('link')) {
      expect(ROLE_RESTRICTED_ROUTES).not.toContain(link.getAttribute('href'))
    }

    expect(navItemsFor({ kind: 'anonymous' }).map((item) => item.to)).toEqual([
      '/',
      '/sign-in',
    ])
  })

  it('exposes the NexusJob wordmark as a link to home', () => {
    renderNavBar()

    expect(screen.getByRole('link', { name: 'NexusJob' })).toHaveAttribute('href', '/')
  })
})
