namespace NexusJob.Modules.Applications.Persistence;

/// <summary>
/// A Job Seeker's application to a job posting (AD-4). Applications is the sole
/// owner. Stored in the <c>applications</c> schema as <c>application</c>;
/// <see cref="JobPostingId"/> and <see cref="JobSeekerId"/> are bare id columns -
/// no foreign key crosses a schema (AD-7). The
/// <c>UNIQUE (job_posting_id, job_seeker_id)</c> constraint (defined here and in
/// no other module) is the guard for idempotent apply (AD-9 / AD-20).
/// </summary>
internal sealed class Application
{
    /// <summary>Primary key. A <see cref="Guid"/> v7 generated in application code (AD-3 conventions).</summary>
    public Guid Id { get; set; }

    /// <summary>The target posting's id. A bare id column - existence is checked via <c>IJobPostingsApi</c> (AD-9). No FK (AD-7).</summary>
    public Guid JobPostingId { get; set; }

    /// <summary>The applying Job Seeker's account id, parsed from the caller's <c>NameIdentifier</c> claim (AD-13). No FK (AD-7).</summary>
    public Guid JobSeekerId { get; set; }

    /// <summary>The UTC instant the application was submitted. Stored <c>timestamptz</c>, serialised ISO-8601 (AD conventions).</summary>
    public DateTimeOffset SubmittedAt { get; set; }
}
