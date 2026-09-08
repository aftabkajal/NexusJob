import { ApiException } from '../api/nexus-api-client'

/**
 * The normalised shape every API-error branch reads. `status` is always
 * present; `title` / `errors` are carried through when the source had them.
 *
 * Moved verbatim from `entities/session/api/toApiError.ts` when a second slice
 * (`entities/job-posting`) needed it — a pure, dependency-free normaliser
 * belongs in `shared/` the moment more than one slice consumes it.
 */
export interface ApiError {
  status: number
  title?: string
  errors?: Record<string, string[]>
}

/**
 * Normalise the two shapes the generated NSwag client can reject with.
 *
 * `throwException` in `nexus-api-client.ts` throws the *parsed RFC 9457 body*
 * (a plain `{ type, title, status, detail, errors? }` object, `status` a number)
 * for any 4xx that carries one — which is every `400` / `401` / `403` / `409`
 * from the `/api/*` surface. It throws an `ApiException` only for bodiless /
 * unexpected failures. So callers must never branch on `instanceof` /
 * `isApiException`; they branch on `toApiError(err)?.status`.
 *
 * Returns `null` for anything that is neither an `ApiException` nor a
 * ProblemDetails-shaped object with a numeric `status`.
 */
export function toApiError(err: unknown): ApiError | null {
  if (err != null && ApiException.isApiException(err)) {
    return { status: err.status, title: err.message }
  }

  if (
    err != null &&
    typeof err === 'object' &&
    typeof (err as { status?: unknown }).status === 'number'
  ) {
    const body = err as { status: number; title?: string; errors?: Record<string, string[]> }
    return { status: body.status, title: body.title, errors: body.errors }
  }

  return null
}
