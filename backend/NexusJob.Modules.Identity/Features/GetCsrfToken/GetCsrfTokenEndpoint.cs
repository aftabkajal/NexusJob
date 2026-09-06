using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Identity.Features.GetCsrfToken;

/// <summary>The <c>GET /api/auth/csrf</c> delegate; it only calls the slice handler (AD-14).</summary>
internal static class GetCsrfTokenEndpoint
{
    public static IResult Handle(
        [FromServices] GetCsrfTokenHandler handler,
        HttpContext httpContext) =>
        handler.Handle(httpContext);
}
