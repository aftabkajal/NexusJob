namespace NexusJob.Modules.Identity.Auth;

/// <summary>
/// Names for the one app-wide auth cookie (AD-13). The scheme and cookie name
/// are referenced by the Host when it registers the cookie handler
/// (app-wide infrastructure, not module wiring - see the spec Design Notes) and
/// by Identity's handlers when they <c>SignInAsync</c> / <c>SignOutAsync</c>.
/// </summary>
public static class AuthCookie
{
    /// <summary>The authentication scheme name.</summary>
    public const string Scheme = "NexusJobAuth";

    /// <summary>The cookie name on the wire.</summary>
    public const string CookieName = "nexusjob_auth";
}
