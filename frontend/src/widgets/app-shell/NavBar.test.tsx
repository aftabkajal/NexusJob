import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'

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

describe('NavBar — signed-in Company viewer', () => {
  function renderCompanyNavBar(onLogOut = vi.fn()) {
    render(
      <MemoryRouter>
        <NavBar viewer={{ kind: 'company', displayName: 'Cobalt Ledger' }} onLogOut={onLogOut} />
      </MemoryRouter>,
    )
    return { onLogOut }
  }

  it('renders Search, the display name, and a Log out control', () => {
    renderCompanyNavBar()

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    expect(
      within(nav)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Search'])
    expect(within(nav).getByText('Cobalt Ledger')).toBeInTheDocument()
    expect(within(nav).getByRole('button', { name: 'Log out' })).toBeInTheDocument()
  })

  it('points no nav link at a Company-only Epic-2 surface', () => {
    renderCompanyNavBar()

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    for (const link of within(nav).getAllByRole('link')) {
      expect(ROLE_RESTRICTED_ROUTES).not.toContain(link.getAttribute('href'))
    }
    expect(navItemsFor({ kind: 'company', displayName: 'Cobalt Ledger' }).map((item) => item.to)).toEqual(
      ['/'],
    )
  })

  it('invokes onLogOut when Log out is activated', async () => {
    const user = userEvent.setup()
    const { onLogOut } = renderCompanyNavBar()

    await user.click(screen.getByRole('button', { name: 'Log out' }))

    expect(onLogOut).toHaveBeenCalledTimes(1)
  })
})

describe('NavBar — signed-in Job Seeker viewer', () => {
  function renderJobSeekerNavBar(onLogOut = vi.fn()) {
    render(
      <MemoryRouter>
        <NavBar viewer={{ kind: 'jobSeeker', displayName: 'Priya Raman' }} onLogOut={onLogOut} />
      </MemoryRouter>,
    )
    return { onLogOut }
  }

  it('resolves navItemsFor to Search only', () => {
    expect(
      navItemsFor({ kind: 'jobSeeker', displayName: 'Priya Raman' }).map((item) => item.to),
    ).toEqual(['/'])
  })

  it('renders Search, the display name, and a Log out control', () => {
    renderJobSeekerNavBar()

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    expect(
      within(nav)
        .getAllByRole('link')
        .map((link) => link.textContent),
    ).toEqual(['Search'])
    expect(within(nav).getByText('Priya Raman')).toBeInTheDocument()
    expect(within(nav).getByRole('button', { name: 'Log out' })).toBeInTheDocument()
  })

  it('points no nav link at a Company-only or Job-Seeker-only surface', () => {
    renderJobSeekerNavBar()

    const nav = screen.getByRole('navigation', { name: 'Primary' })
    for (const link of within(nav).getAllByRole('link')) {
      expect(ROLE_RESTRICTED_ROUTES).not.toContain(link.getAttribute('href'))
    }
  })

  it('invokes onLogOut when Log out is activated', async () => {
    const user = userEvent.setup()
    const { onLogOut } = renderJobSeekerNavBar()

    await user.click(screen.getByRole('button', { name: 'Log out' }))

    expect(onLogOut).toHaveBeenCalledTimes(1)
  })
})
