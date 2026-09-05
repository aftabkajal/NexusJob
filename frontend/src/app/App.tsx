import { APP_NAME } from '../shared'

// Minimal placeholder. The real role-aware shell (nav bar, Home / Search route,
// Sign up / Log in surface) is story 1.2. This import (app -> shared) exercises
// a downward, allowed FSD edge so `npm run lint` has something real to check.
export function App() {
  return (
    <main>
      <h1>{APP_NAME}</h1>
      <p>Walking skeleton. The application shell arrives in story 1.2.</p>
    </main>
  )
}
