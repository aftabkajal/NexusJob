namespace NexusJob.Modules.JobPostings.Features.GetMyJobPostings;

/// <summary>
/// One row of <c>GET /api/job-postings/mine</c>'s paged results - one shape, no
/// envelope (AD-15). Serialised as <c>{ id, title, description, createdAt }</c>.
/// Posting fields only - no applicant counts, no company name (the caller already
/// knows which Company it is; spec Boundaries &amp; Constraints).
/// </summary>
public sealed record JobPostingMineItemResponse(
    string Id,
    string Title,
    string Description,
    DateTimeOffset CreatedAt);
