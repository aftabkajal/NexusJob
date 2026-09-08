// FSD `entities` layer: domain nouns with their query keys and API segments.
export {
  authClient,
  sessionQueryKey,
  useSession,
  type SessionViewer,
} from './session'
export {
  jobPostingQueryKey,
  jobPostingsClient,
  useJobPosting,
  type CreateJobPostingRequest,
  type JobPostingDetailResponse,
  type JobPostingResponse,
} from './job-posting'
