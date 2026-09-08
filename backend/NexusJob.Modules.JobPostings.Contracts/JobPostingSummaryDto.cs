namespace NexusJob.Modules.JobPostings.Contracts;

/// <summary>
/// The cross-module summary of a job posting (AD-19). Posting-owned fields only -
/// no company name: a consumer that needs the owning Company's display name calls
/// <c>IIdentityApi.GetCompanies</c> itself (AD-19 batch-compose). A named DTO,
/// never the <c>JobPosting</c> entity; all fields non-null.
/// </summary>
/// <param name="Id">The posting id.</param>
/// <param name="Title">The posting title.</param>
/// <param name="Description">The posting description.</param>
/// <param name="OwnerCompanyId">The owning Company's account id (a bare id - no FK, AD-7).</param>
/// <param name="CreatedAt">The UTC instant the posting was created.</param>
public sealed record JobPostingSummaryDto(
    Guid Id,
    string Title,
    string Description,
    Guid OwnerCompanyId,
    DateTimeOffset CreatedAt);
