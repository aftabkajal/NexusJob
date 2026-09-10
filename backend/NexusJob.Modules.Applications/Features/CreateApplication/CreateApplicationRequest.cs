using System.ComponentModel.DataAnnotations;

namespace NexusJob.Modules.Applications.Features.CreateApplication;

/// <summary>
/// Body of <c>POST /api/applications</c>. Validated by the module's
/// <see cref="Auth.DataAnnotationsValidationFilter{TRequest}"/> (AD-15); a failure
/// is a <c>400</c> validation ProblemDetails naming <c>jobPostingId</c>, and no
/// row is written.
///
/// <see cref="JobPostingId"/> is a <c>string</c>, parsed by the handler's
/// <c>Guid.TryParse</c> (spec Design Notes): binding a <c>Guid</c> directly would
/// surface a malformed value as a framework <c>BadHttpRequestException</c> rather
/// than a deterministic <c>400</c> validation ProblemDetails naming the field.
/// <see cref="RequiredAttribute"/> with <c>AllowEmptyStrings = false</c> rejects
/// an empty or whitespace-only value; <c>[StringLength(68)]</c> bounds it while
/// still admitting every <see cref="System.Guid"/> string format (the braced and
/// parenthesized forms are 38 chars); the handler's <c>Guid.TryParse</c> is the
/// real check.
/// </summary>
public sealed class CreateApplicationRequest
{
    /// <summary>The target posting's id, as a GUID string. Non-empty; parsed in the handler.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(68)]
    public string JobPostingId { get; init; } = "";
}
