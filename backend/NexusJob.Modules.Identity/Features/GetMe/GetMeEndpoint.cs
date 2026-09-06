using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Identity.Features.GetMe;

/// <summary>The <c>GET /api/auth/me</c> delegate; it only calls the slice handler (AD-14).</summary>
internal static class GetMeEndpoint
{
    public static Task<IResult> Handle(
        [FromServices] GetMeHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(httpContext, cancellationToken);
}
