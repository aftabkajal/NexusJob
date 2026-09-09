namespace NexusJob.Modules.JobPostings.Features.SearchJobPostings;

/// <summary>
/// One row of <c>GET /api/job-postings</c>'s search results - one shape, no
/// envelope (AD-15). Serialised as <c>{ id, title, description, companyName }</c>;
/// <c>id</c> is the posting <see cref="System.Guid"/> as a string (AD-15
/// conventions), matching 2.2b's precedent of no posted-date on the surface.
/// <c>companyName</c> is the owning Company's display name, resolved through the
/// batched <c>IIdentityApi.GetCompanies</c> call (AD-5 / AD-19).
/// </summary>
public sealed record JobPostingSearchResultResponse(string Id, string Title, string Description, string CompanyName);
