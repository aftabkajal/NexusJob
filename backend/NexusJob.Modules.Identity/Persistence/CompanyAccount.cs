namespace NexusJob.Modules.Identity.Persistence;

/// <summary>
/// A registered Company account (AD-4, AD-11). Email is unique within this table
/// only. Stored in the <c>identity</c> schema as <c>company_account</c>; no
/// foreign key crosses a schema (AD-7).
/// </summary>
internal sealed class CompanyAccount
{
    /// <summary>Primary key. A <see cref="Guid"/> v7 generated in application code (AD-3 conventions).</summary>
    public Guid Id { get; set; }

    /// <summary>Trimmed, lower-cased email. The unique index is on this stored value.</summary>
    public required string Email { get; set; }

    /// <summary>PBKDF2 hash produced by <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>. Never the plaintext.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>The Company's display name (the "Company name" field at registration).</summary>
    public required string DisplayName { get; set; }
}
