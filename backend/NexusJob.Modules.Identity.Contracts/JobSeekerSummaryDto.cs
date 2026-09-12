namespace NexusJob.Modules.Identity.Contracts;

/// <summary>
/// The cross-module summary of a Job Seeker account (AD-19 exact shape). A named
/// DTO, never the <c>JobSeekerAccount</c> entity; <see cref="Id"/> is a
/// <see cref="Guid"/> in the C# signature (string only at the JSON edge). All
/// fields are non-null. <see cref="Email"/> is exposed only on this DTO and no
/// other Contract DTO (epics.md) - FR-7's applicant list is the only consumer
/// that needs it.
/// </summary>
/// <param name="Id">The Job Seeker account id.</param>
/// <param name="FullName">The Job Seeker's full name (the "Full name" field at registration).</param>
/// <param name="Email">The Job Seeker's email.</param>
public sealed record JobSeekerSummaryDto(Guid Id, string FullName, string Email);
