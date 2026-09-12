// FSD `entities/application` — the Application noun (the AR-10 bounded-context
// mirror of the backend `Applications` module): the configured
// `ApplicationsClient` wrapper, its request / response types, and the `mine`
// applied-state query with its key.
//
// This slice may NOT import `entities/session` or `entities/job-posting`
// (FSD forbids sibling `entities` imports); the `ApplyButton`, which needs
// both, lives in `features/apply-to-posting`.
export {
  applicationsClient,
  type ApplicationResponse,
  type CreateApplicationRequest,
  type MyApplicationListItemResponse,
  type MyApplicationResponse,
  type PageOfMyApplicationListItemResponse,
} from './api/applicationsClient'
export {
  applicationMineQueryKey,
  applicationsMineListQueryKey,
  useMyApplication,
  useMyApplications,
} from './model/applicationQuery'
