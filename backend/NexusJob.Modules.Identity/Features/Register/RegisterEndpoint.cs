using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Identity.Features.Register;

/// <summary>The <c>POST /api/auth/register</c> delegate; it only calls the slice handler (AD-14).</summary>
internal static class RegisterEndpoint
{
    public static Task<IResult> Handle(
        [FromBody] RegisterRequest request,
        [FromServices] RegisterHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, httpContext, cancellationToken);
}
