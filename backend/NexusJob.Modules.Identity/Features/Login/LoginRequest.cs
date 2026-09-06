using System.ComponentModel.DataAnnotations;

namespace NexusJob.Modules.Identity.Features.Login;

/// <summary>
/// Body of <c>POST /api/auth/login</c>. Shape-only validation here (both fields
/// present); a credential mismatch is decided by the handler and always returns
/// the same generic <c>401</c> (spec Design Notes).
/// </summary>
public sealed class LoginRequest
{
    /// <summary>Must be <c>"company"</c> in story 1.3a.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(32)]
    public string AccountType { get; init; } = string.Empty;

    /// <summary>Login email; matched case-insensitively against the stored normalised value.</summary>
    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    [StringLength(320)]
    public string Email { get; init; } = string.Empty;

    /// <summary>Plaintext password; verified against the stored PBKDF2 hash, never logged.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(256)]
    public string Password { get; init; } = string.Empty;
}
