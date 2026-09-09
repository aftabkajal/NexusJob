import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { Pagination } from './Pagination'

describe('Pagination', () => {
  it('wraps the control in a nav landmark labelled "Search results pages"', () => {
    render(<Pagination page={1} pageSize={20} total={100} onPageChange={vi.fn()} />)

    expect(screen.getByRole('navigation', { name: 'Search results pages' })).toBeInTheDocument()
  })

  it('renders "Page {page} of {totalPages}" between Prev and Next', () => {
    render(<Pagination page={2} pageSize={20} total={100} onPageChange={vi.fn()} />)

    expect(screen.getByText('Page 2 of 5')).toBeInTheDocument()
  })

  it('disables Prev at page 1 and enables Next', () => {
    render(<Pagination page={1} pageSize={20} total={100} onPageChange={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Prev' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled()
  })

  it('disables Next at the last page and enables Prev', () => {
    render(<Pagination page={5} pageSize={20} total={100} onPageChange={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Prev' })).toBeEnabled()
  })

  it('renders both buttons disabled when there is only one page', () => {
    render(<Pagination page={1} pageSize={20} total={5} onPageChange={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Prev' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(screen.getByText('Page 1 of 1')).toBeInTheDocument()
  })

  it('calls onPageChange with the next page number on Next click', async () => {
    const user = userEvent.setup()
    const onPageChange = vi.fn()
    render(<Pagination page={2} pageSize={20} total={100} onPageChange={onPageChange} />)

    await user.click(screen.getByRole('button', { name: 'Next' }))

    expect(onPageChange).toHaveBeenCalledWith(3)
  })

  it('calls onPageChange with the previous page number on Prev click', async () => {
    const user = userEvent.setup()
    const onPageChange = vi.fn()
    render(<Pagination page={2} pageSize={20} total={100} onPageChange={onPageChange} />)

    await user.click(screen.getByRole('button', { name: 'Prev' }))

    expect(onPageChange).toHaveBeenCalledWith(1)
  })
})
