import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { JobPostingCardSkeleton } from './JobPostingCardSkeleton'

describe('JobPostingCardSkeleton', () => {
  it('renders with aria-hidden="true"', () => {
    const { container } = render(<JobPostingCardSkeleton />)

    expect(container.firstChild).toHaveAttribute('aria-hidden', 'true')
  })

  it('renders no text content', () => {
    const { container } = render(<JobPostingCardSkeleton />)

    expect(container.textContent).toBe('')
  })
})
