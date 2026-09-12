namespace NexusJob.Modules.Applications.Features.GetApplicants;

/// <summary>
/// One row of <c>GET /api/applications</c>'s paged applicant results - one
/// shape, no envelope (AD-15). Serialised as
/// <c>{ jobSeekerId, fullName, email, submittedAt }</c>. <c>fullName</c>/
/// <c>email</c> are the applicant's identity, resolved through the batched
/// <c>IIdentityApi.GetJobSeekers</c> call for the whole page (AD-5 / AD-19). No
/// posting fields - the caller already has the posting from the page it drilled
/// in from (spec Boundaries &amp; Constraints).
/// </summary>
public sealed record ApplicantListItemResponse(
    string JobSeekerId,
    string FullName,
    string Email,
    DateTimeOffset SubmittedAt);
