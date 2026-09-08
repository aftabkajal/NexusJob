namespace NexusJob.Modules.Identity.Contracts;

/// <summary>
/// The cross-module summary of a Company account (AD-19 exact shape). A named DTO,
/// never the <c>CompanyAccount</c> entity; <see cref="Id"/> is a <see cref="Guid"/>
/// in the C# signature (string only at the JSON edge). Both fields are non-null.
/// </summary>
/// <param name="Id">The Company account id.</param>
/// <param name="DisplayName">The Company's display name (the "Company name" field at registration).</param>
public sealed record CompanySummaryDto(Guid Id, string DisplayName);
