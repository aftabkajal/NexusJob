namespace NexusJob.Modules.JobPostings.Persistence;

/// <summary>
/// A job posting created by a Company (AD-4). JobPostings is the sole owner.
/// Stored in the <c>job_postings</c> schema as <c>job_posting</c>;
/// <see cref="OwnerCompanyId"/> is a bare id column - no foreign key crosses a
/// schema (AD-7).
/// </summary>
internal sealed class JobPosting
{
    /// <summary>Primary key. A <see cref="Guid"/> v7 generated in application code (AD-3 conventions).</summary>
    public Guid Id { get; set; }

    /// <summary>The owning Company's account id, parsed from the caller's <c>NameIdentifier</c> claim (AD-13). No FK (AD-7).</summary>
    public Guid OwnerCompanyId { get; set; }

    /// <summary>The posting title. Trimmed before storage; never empty or whitespace (validation filter).</summary>
    public required string Title { get; set; }

    /// <summary>The posting description. Trimmed before storage; never empty or whitespace (validation filter).</summary>
    public required string Description { get; set; }

    /// <summary>The UTC instant the posting was created. Stored <c>timestamptz</c>, serialised ISO-8601 (AD conventions).</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
