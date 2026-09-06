using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using NexusJob.Modules.Identity.Auth;

namespace NexusJob.Modules.Identity.Features.Logout;

/// <summary>
/// Clears the auth cookie (epic context). <c>204 No Content</c>; the response
/// carries the cookie-clearing <c>Set-Cookie</c>, so a subsequent
/// <c>GET /api/auth/me</c> is <c>401</c>. Only Identity's slice clears this
/// cookie (AD-13).
/// </summary>
internal sealed class LogoutHandler
{
    public async Task<IResult> HandleAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(AuthCookie.Scheme);
        return Results.NoContent();
    }
}
