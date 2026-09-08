// FSD `entities/job-posting` — the Job Posting noun: the configured
// `JobPostingsClient`, its request / response types, and the detail query.
export {
  jobPostingsClient,
  type CreateJobPostingRequest,
  type JobPostingDetailResponse,
  type JobPostingResponse,
} from './api/jobPostingsClient'
export { jobPostingQueryKey, useJobPosting } from './model/jobPostingQuery'
