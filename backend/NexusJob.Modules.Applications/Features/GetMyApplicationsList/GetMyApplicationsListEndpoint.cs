using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.Applications.Features.GetMyApplicationsList;

/// <summary>
/// The <c>GET /api/applications/mine/list</c> delegate; it only calls the slice
/// handler (AD-14). A Job Seeker session is required
/// (<c>RequireAuthorization()</c> + <see cref="Auth.JobSeekerOnlyEndpointFilter"/>);
/// no antiforgery on a GET. A sibling route of the existing
/// <c>GET /api/applications/mine</c> probe, not a second shape on it (spec
/// Boundaries &amp; Constraints, Decision 2026-09-12) - a distinct operation id,
/// <c>Applications_GetMyApplications</c>.
/// </summary>
internal static class GetMyApplicationsListEndpoint
{
    public static Task<IResult> Handle(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] GetMyApplicationsListHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(page, pageSize, httpContext, cancellationToken);
}
