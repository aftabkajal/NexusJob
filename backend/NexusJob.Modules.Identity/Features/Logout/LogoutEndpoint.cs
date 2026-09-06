using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Identity.Features.Logout;

/// <summary>The <c>POST /api/auth/logout</c> delegate; it only calls the slice handler (AD-14).</summary>
internal static class LogoutEndpoint
{
    public static Task<IResult> Handle(
        [FromServices] LogoutHandler handler,
        HttpContext httpContext) =>
        handler.HandleAsync(httpContext);
}
