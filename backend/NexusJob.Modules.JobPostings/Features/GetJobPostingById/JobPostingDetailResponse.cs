namespace NexusJob.Modules.JobPostings.Features.GetJobPostingById;

/// <summary>
/// The representation returned by <c>GET /api/job-postings/{id}</c> - one shape,
/// no envelope (AD-15). Serialised as <c>{ id, title, description, companyName }</c>;
/// <c>id</c> is the posting <see cref="System.Guid"/> as a string (AD-15
/// conventions) and <c>companyName</c> is the owning Company's display name,
/// resolved through <c>IIdentityApi</c> (AD-5 / AD-19).
/// </summary>
public sealed record JobPostingDetailResponse(string Id, string Title, string Description, string CompanyName);
