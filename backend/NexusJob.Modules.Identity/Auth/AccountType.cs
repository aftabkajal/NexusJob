namespace NexusJob.Modules.Identity.Auth;

/// <summary>
/// The <c>account_type</c> claim values and helpers (AD-11, AD-13). Story 1.3a
/// serves only <see cref="Company"/>; <c>job_seeker</c> is story 1.4.
/// </summary>
internal static class AccountType
{
    /// <summary>The claim type carrying the account type on the auth cookie.</summary>
    public const string ClaimType = "account_type";

    /// <summary>A Company account.</summary>
    public const string Company = "company";

    /// <summary>
    /// True when <paramref name="value"/> names a Company account. Trimmed and
    /// case-insensitive so a client that sends <c>"Company"</c> still matches.
    /// </summary>
    public static bool IsCompany(string? value) =>
        string.Equals(value?.Trim(), Company, StringComparison.OrdinalIgnoreCase);
}
