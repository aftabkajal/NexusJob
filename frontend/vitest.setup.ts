import { afterEach } from 'vitest'

import { cleanup } from '@testing-library/react'
import '@testing-library/jest-dom/vitest'

// Unmount anything a test rendered, so DOM assertions never see a prior test's tree.
afterEach(() => {
  cleanup()
})
