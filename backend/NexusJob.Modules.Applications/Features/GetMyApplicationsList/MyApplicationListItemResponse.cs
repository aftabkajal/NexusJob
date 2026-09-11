namespace NexusJob.Modules.Applications.Features.GetMyApplicationsList;

/// <summary>
/// One row of <c>GET /api/applications/mine/list</c>'s paged results - one
/// shape, no envelope (AD-15). Serialised as
/// <c>{ applicationId, jobPostingId, jobPostingTitle, submittedAt }</c>.
/// <c>jobPostingTitle</c> is the posting's title, resolved through the batched
/// <c>IJobPostingsApi.GetPostingSummaries</c> call for the whole page (AD-5 /
/// AD-19). No company name, description, or status - out of this epic's scope
/// (epic-3-context; spec Boundaries &amp; Constraints).
/// </summary>
public sealed record MyApplicationListItemResponse(
    string ApplicationId,
    string JobPostingId,
    string JobPostingTitle,
    DateTimeOffset SubmittedAt);
