namespace NexusJob.Modules.Identity.Persistence;

/// <summary>
/// A registered Job Seeker account (AD-4, AD-11). Email is unique within this
/// table only. Stored in the <c>identity</c> schema as <c>job_seeker_account</c>;
/// it has no relationship to <see cref="CompanyAccount"/> and no foreign key
/// crosses a schema (AD-7). The same email may hold one Company and one Job
/// Seeker account as unrelated identities.
/// </summary>
internal sealed class JobSeekerAccount
{
    /// <summary>Primary key. A <see cref="Guid"/> v7 generated in application code (AD-3 conventions).</summary>
    public Guid Id { get; set; }

    /// <summary>Trimmed, lower-cased email. The unique index is on this stored value.</summary>
    public required string Email { get; set; }

    /// <summary>PBKDF2 hash produced by <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>. Never the plaintext.</summary>
    public required string PasswordHash { get; set; }

    /// <summary>The Job Seeker's full name (the "Full name" field at registration).</summary>
    public required string FullName { get; set; }
}
