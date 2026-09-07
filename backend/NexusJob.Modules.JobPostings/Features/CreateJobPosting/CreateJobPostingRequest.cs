using System.ComponentModel.DataAnnotations;

namespace NexusJob.Modules.JobPostings.Features.CreateJobPosting;

/// <summary>
/// Body of <c>POST /api/job-postings</c>. Validated by the module's
/// <see cref="Auth.DataAnnotationsValidationFilter{TRequest}"/> (AD-15); a failure
/// is a <c>400</c> validation ProblemDetails naming the invalid fields, and no row
/// is written. <see cref="RequiredAttribute"/> with
/// <c>AllowEmptyStrings = false</c> trims first, so a whitespace-only value fails.
/// </summary>
public sealed class CreateJobPostingRequest
{
    /// <summary>The posting title. Non-empty, non-whitespace; stored trimmed.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = string.Empty;

    /// <summary>The posting description. Non-empty, non-whitespace; stored trimmed.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;
}
