using System.ComponentModel.DataAnnotations;

namespace NexusJob.Modules.Identity.Features.Register;

/// <summary>
/// Body of <c>POST /api/auth/register</c>. Validated by the built-in minimal-API
/// DataAnnotations validation (AD-15); a failure is a <c>400</c> validation
/// ProblemDetails naming the invalid fields, and no row is written.
/// </summary>
public sealed class RegisterRequest
{
    /// <summary>Must be <c>"company"</c> in story 1.3a; anything else is rejected (no Job Seeker path yet).</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(32)]
    public string AccountType { get; init; } = string.Empty;

    /// <summary>The Company name. Becomes <see cref="AuthAccountResponse.DisplayName"/>.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Login email. Unique within Company accounts, case-insensitively.</summary>
    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Plaintext password; hashed with PBKDF2 before storage, never logged.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(256, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;
}
