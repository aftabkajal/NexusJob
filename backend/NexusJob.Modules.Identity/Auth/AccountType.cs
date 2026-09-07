namespace NexusJob.Modules.Identity.Auth;

/// <summary>
/// The <c>account_type</c> claim values and helpers (AD-11, AD-13). Both
/// <see cref="Company"/> and <see cref="JobSeeker"/> are served; the two account
/// spaces are fully independent (spec 1.4a).
/// </summary>
internal static class AccountType
{
    /// <summary>The claim type carrying the account type on the auth cookie.</summary>
    public const string ClaimType = "account_type";

    /// <summary>A Company account.</summary>
    public const string Company = "company";

    /// <summary>A Job Seeker account.</summary>
    public const string JobSeeker = "job_seeker";

    /// <summary>
    /// True when <paramref name="value"/> names a Company account. Trimmed and
    /// case-insensitive so a client that sends <c>"Company"</c> still matches.
    /// </summary>
    public static bool IsCompany(string? value) =>
        string.Equals(value?.Trim(), Company, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when <paramref name="value"/> names a Job Seeker account. Trimmed and
    /// case-insensitive, mirroring <see cref="IsCompany"/>.
    /// </summary>
    public static bool IsJobSeeker(string? value) =>
        string.Equals(value?.Trim(), JobSeeker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a raw <c>accountType</c> value to the canonical
    /// <see cref="Company"/> / <see cref="JobSeeker"/> constant, or <c>null</c>
    /// when it names neither. The three <c>/api/auth/*</c> handlers branch on this.
    /// </summary>
    public static string? Classify(string? value) =>
        IsCompany(value) ? Company
        : IsJobSeeker(value) ? JobSeeker
        : null;
}
