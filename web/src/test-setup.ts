import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Unmount what each test rendered (automatic only with vitest globals).
afterEach(cleanup)
