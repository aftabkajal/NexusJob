namespace NexusJob.Modules.Identity.Auth;

/// <summary>
/// One normalisation rule for account emails (spec Design Notes): trim, then
/// <see cref="string.ToLowerInvariant"/>. The unique index is on the stored
/// normalised value, and login looks up by the same normalised value, so
/// registration is case-insensitive without <c>citext</c> or a functional index.
/// </summary>
internal static class EmailNormalizer
{
    public static string Normalize(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToLowerInvariant();
    }
}
