import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { RoleToggle } from './RoleToggle'

describe('RoleToggle', () => {
  it('renders a radiogroup with "Job Seeker" active by default', () => {
    render(<RoleToggle />)

    expect(screen.getByRole('radiogroup')).toBeInTheDocument()
    expect(screen.getByRole('radio', { name: 'Job Seeker' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Company' })).not.toBeChecked()
  })

  it('keeps a single tab stop via roving tabindex', () => {
    render(<RoleToggle />)

    expect(screen.getByRole('radio', { name: 'Job Seeker' })).toHaveAttribute('tabindex', '0')
    expect(screen.getByRole('radio', { name: 'Company' })).toHaveAttribute('tabindex', '-1')
  })

  it('moves the active option with ArrowRight / ArrowLeft and reflects it in aria-checked', async () => {
    const user = userEvent.setup()
    render(<RoleToggle />)
    const company = screen.getByRole('radio', { name: 'Company' })
    const jobSeeker = screen.getByRole('radio', { name: 'Job Seeker' })

    jobSeeker.focus()
    await user.keyboard('{ArrowRight}')

    expect(company).toBeChecked()
    expect(company).toHaveAttribute('aria-checked', 'true')
    expect(jobSeeker).toHaveAttribute('aria-checked', 'false')
    expect(company).toHaveFocus()

    await user.keyboard('{ArrowLeft}')

    expect(jobSeeker).toBeChecked()
    expect(company).toHaveAttribute('aria-checked', 'false')
  })

  it('fires onChange with the new role on arrow move and on Enter / Space confirm', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<RoleToggle onChange={onChange} />)

    screen.getByRole('radio', { name: 'Job Seeker' }).focus()

    await user.keyboard('{Enter}')
    expect(onChange).toHaveBeenLastCalledWith('jobSeeker')

    await user.keyboard('{ArrowRight}')
    expect(onChange).toHaveBeenLastCalledWith('company')

    onChange.mockClear()
    await user.keyboard(' ')
    expect(onChange).toHaveBeenCalledWith('company')
  })

  it('supports controlled use — the value prop drives the active option', () => {
    render(<RoleToggle value="company" />)

    expect(screen.getByRole('radio', { name: 'Company' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Job Seeker' })).not.toBeChecked()
  })

  it('seeds the uncontrolled selection from defaultValue', () => {
    render(<RoleToggle defaultValue="company" />)

    expect(screen.getByRole('radio', { name: 'Company' })).toBeChecked()
    expect(screen.getByRole('radio', { name: 'Job Seeker' })).not.toBeChecked()
  })

  describe('disabledValues', () => {
    it('marks a disabled option with aria-disabled and never a tab stop', () => {
      render(<RoleToggle defaultValue="company" disabledValues={['jobSeeker']} />)

      const jobSeeker = screen.getByRole('radio', { name: 'Job Seeker' })
      expect(jobSeeker).toHaveAttribute('aria-disabled', 'true')
      expect(jobSeeker).toHaveAttribute('tabindex', '-1')
    })

    it('does not select a disabled option on click', async () => {
      const user = userEvent.setup()
      const onChange = vi.fn()
      render(
        <RoleToggle defaultValue="company" disabledValues={['jobSeeker']} onChange={onChange} />,
      )

      await user.click(screen.getByRole('radio', { name: 'Job Seeker' }))

      expect(screen.getByRole('radio', { name: 'Company' })).toBeChecked()
      expect(screen.getByRole('radio', { name: 'Job Seeker' })).not.toBeChecked()
      expect(onChange).not.toHaveBeenCalled()
    })

    it('skips a disabled option during arrow-key navigation', async () => {
      const user = userEvent.setup()
      const onChange = vi.fn()
      render(
        <RoleToggle defaultValue="company" disabledValues={['jobSeeker']} onChange={onChange} />,
      )
      const company = screen.getByRole('radio', { name: 'Company' })
      company.focus()

      await user.keyboard('{ArrowRight}')

      expect(company).toBeChecked()
      expect(screen.getByRole('radio', { name: 'Job Seeker' })).not.toBeChecked()
      expect(onChange).not.toHaveBeenCalled()
    })
  })
})
