import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'

import { JobPostingCard } from './JobPostingCard'

function renderCard() {
  return render(
    <MemoryRouter>
      <JobPostingCard
        id="jp-1"
        title="Staff Engineer"
        companyName="Cobalt Ledger"
        description="Build the platform."
      />
    </MemoryRouter>,
  )
}

describe('JobPostingCard', () => {
  it('renders the title, company name, and description', () => {
    renderCard()

    expect(screen.getByText('Staff Engineer')).toBeInTheDocument()
    expect(screen.getByText('Cobalt Ledger')).toBeInTheDocument()
    expect(screen.getByText('Build the platform.')).toBeInTheDocument()
  })

  it('is a single whole-card link to the posting detail view', () => {
    renderCard()

    const links = screen.getAllByRole('link')
    expect(links).toHaveLength(1)
    expect(links[0]).toHaveAttribute('href', '/job-postings/jp-1')
    expect(links[0]).toHaveTextContent('Staff Engineer')
    expect(links[0]).toHaveTextContent('Cobalt Ledger')
    expect(links[0]).toHaveTextContent('Build the platform.')
  })

  it('renders no Apply control (Epic 3 scope)', () => {
    renderCard()

    expect(screen.queryByRole('button', { name: /apply/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /apply/i })).not.toBeInTheDocument()
  })
})
