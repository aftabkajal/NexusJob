using System.Security.Claims;

namespace NexusJob.Modules.Identity.Auth;

/// <summary>
/// Builds the <see cref="ClaimsPrincipal"/> that Identity signs into the auth
/// cookie (AD-13). Only two claims: <see cref="ClaimTypes.NameIdentifier"/> =
/// the account id, and <see cref="AccountType.ClaimType"/> = the account type.
/// "Who is calling" is always read back from <c>NameIdentifier</c> - never a
/// display-name or email claim - so a later display-name change needs no
/// re-issued cookie.
/// </summary>
internal static class ClaimsPrincipalFactory
{
    public static ClaimsPrincipal ForCompany(Guid accountId) =>
        For(accountId, AccountType.Company);

    public static ClaimsPrincipal ForJobSeeker(Guid accountId) =>
        For(accountId, AccountType.JobSeeker);

    private static ClaimsPrincipal For(Guid accountId, string accountType)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
                new Claim(AccountType.ClaimType, accountType),
            ],
            AuthCookie.Scheme,
            nameType: ClaimTypes.NameIdentifier,
            roleType: ClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
