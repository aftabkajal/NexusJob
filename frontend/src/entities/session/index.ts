// FSD `entities/session` — the signed-in viewer: the configured auth client and
// the `me` query with its key. The `toApiError` / `ApiError` normaliser now
// lives in `shared/lib` (a second slice consumes it).
export { authClient } from './api/authClient'
export { sessionQueryKey, useSession, type SessionViewer } from './model/sessionQuery'
