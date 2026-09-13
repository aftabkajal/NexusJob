// FSD `entities/job-posting` — the Job Posting noun: the configured
// `JobPostingsClient`, its request / response types, the detail query, the
// search query, the `mine` query, and the card / card-skeleton components.
export {
  jobPostingsClient,
  type CreateJobPostingRequest,
  type JobPostingDetailResponse,
  type JobPostingMineItemResponse,
  type JobPostingResponse,
  type JobPostingSearchResultResponse,
  type PageOfJobPostingMineItemResponse,
  type PageOfJobPostingSearchResultResponse,
} from './api/jobPostingsClient'
export {
  jobPostingQueryKey,
  jobPostingSearchQueryKey,
  jobPostingsMineQueryKey,
  useJobPosting,
  useJobPostingSearch,
  useMyJobPostings,
} from './model/jobPostingQuery'
export { JobPostingCard, JobPostingCardSkeleton } from './ui'
