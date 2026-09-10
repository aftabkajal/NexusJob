using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Applications.Features.GetMyApplication;

/// <summary>
/// The <c>GET /api/applications/mine</c> delegate; it only calls the slice
/// handler (AD-14). A Job Seeker session is required
/// (<c>RequireAuthorization()</c> + <see cref="Auth.JobSeekerOnlyEndpointFilter"/>);
/// no antiforgery on a GET.
///
/// <paramref name="jobPostingId"/> is bound as a nullable <c>string</c> so the
/// handler owns the "missing or unparseable" case as a <c>400</c> validation
/// ProblemDetails rather than a framework bad-request (spec Design Notes /
/// I/O matrix). <see cref="RequiredAttribute"/> marks it required in the OpenAPI
/// document (operation <c>Applications_GetMine</c>). Story 3.3 adds a
/// no-<c>jobPostingId</c> paged-list form on this same route, so the parameter
/// handling is kept narrow here.
/// </summary>
internal static class GetMyApplicationEndpoint
{
    public static Task<IResult> Handle(
        [FromQuery][Required] string? jobPostingId,
        [FromServices] GetMyApplicationHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(jobPostingId, httpContext, cancellationToken);
}
