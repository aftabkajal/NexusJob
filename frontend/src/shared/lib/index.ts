// FSD `shared/lib` — pure, dependency-free helpers with no domain knowledge.
export { toApiError, type ApiError } from './toApiError'
export {
  EMAIL_PATTERN,
  mapAuthFieldErrors,
  validateEmail,
  validateName,
  validatePassword,
  type AuthFieldErrors,
} from './authValidation'
