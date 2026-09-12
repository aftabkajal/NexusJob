using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.JobPostings.Features.GetMyJobPostings;

/// <summary>
/// The <c>GET /api/job-postings/mine</c> delegate; it only calls the slice
/// handler (AD-14). Company-only (<c>RequireAuthorization()</c> +
/// <see cref="Auth.CompanyOnlyEndpointFilter"/>); no antiforgery on a GET.
/// </summary>
internal static class GetMyJobPostingsEndpoint
{
    public static Task<IResult> Handle(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] GetMyJobPostingsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(page, pageSize, httpContext, cancellationToken);
}
