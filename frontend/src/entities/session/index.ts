// FSD `entities/session` — the signed-in viewer: the configured auth client, the
// `me` query and its key, and the error normaliser every auth branch reads.
export { authClient } from './api/authClient'
export { toApiError, type ApiError } from './api/toApiError'
export { sessionQueryKey, useSession, type SessionViewer } from './model/sessionQuery'
