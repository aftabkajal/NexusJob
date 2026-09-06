import { QueryClient } from '@tanstack/react-query'

/**
 * The single `QueryClient` for the app (AD-16). Created here in `app/` and
 * handed down through `<QueryClientProvider>`; nothing below `app/` imports this
 * singleton — code reaches the client only via `useQueryClient()` / query hooks.
 *
 * `retry: false` because the one query this app runs on load is
 * `GET /api/auth/me`, whose `401` (signed-out) is an expected answer, not a
 * transient failure worth retrying.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
    },
  },
})
