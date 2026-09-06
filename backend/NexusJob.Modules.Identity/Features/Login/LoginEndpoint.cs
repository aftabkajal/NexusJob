using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Identity.Features.Login;

/// <summary>The <c>POST /api/auth/login</c> delegate; it only calls the slice handler (AD-14).</summary>
internal static class LoginEndpoint
{
    public static Task<IResult> Handle(
        [FromBody] LoginRequest request,
        [FromServices] LoginHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, httpContext, cancellationToken);
}
