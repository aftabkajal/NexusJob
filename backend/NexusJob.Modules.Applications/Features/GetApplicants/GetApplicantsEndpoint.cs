using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Applications.Features.GetApplicants;

/// <summary>
/// The <c>GET /api/applications?jobPostingId=</c> delegate; it only calls the
/// slice handler (AD-14). Company-only (<c>RequireAuthorization()</c> +
/// <see cref="Auth.CompanyOnlyEndpointFilter"/>); no antiforgery on a GET.
///
/// <paramref name="jobPostingId"/> is bound as a nullable <c>string</c> so the
/// handler owns the "missing or unparseable" case as a <c>400</c> validation
/// ProblemDetails naming <c>jobPostingId</c> rather than a framework
/// bad-request, mirroring <c>Applications_GetMine</c> (spec Boundaries &amp;
/// Constraints). <see cref="RequiredAttribute"/> marks it required in the
/// OpenAPI document (operation <c>Applications_GetApplicants</c>).
/// </summary>
internal static class GetApplicantsEndpoint
{
    public static Task<IResult> Handle(
        [FromQuery][Required] string? jobPostingId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] GetApplicantsHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(jobPostingId, page, pageSize, httpContext, cancellationToken);
}
