// FSD `entities/job-posting` — the Job Posting noun: the configured
// `JobPostingsClient`, its request / response types, the detail query, the
// search query, and the card / card-skeleton components.
export {
  jobPostingsClient,
  type CreateJobPostingRequest,
  type JobPostingDetailResponse,
  type JobPostingResponse,
  type JobPostingSearchResultResponse,
  type PageOfJobPostingSearchResultResponse,
} from './api/jobPostingsClient'
export {
  jobPostingQueryKey,
  jobPostingSearchQueryKey,
  useJobPosting,
  useJobPostingSearch,
} from './model/jobPostingQuery'
export { JobPostingCard, JobPostingCardSkeleton } from './ui'
