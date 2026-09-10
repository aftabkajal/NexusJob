using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Applications.Features.CreateApplication;

/// <summary>The <c>POST /api/applications</c> delegate; it only calls the slice handler (AD-14).</summary>
internal static class CreateApplicationEndpoint
{
    public static Task<IResult> Handle(
        [FromBody] CreateApplicationRequest request,
        [FromServices] CreateApplicationHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, httpContext, cancellationToken);
}
