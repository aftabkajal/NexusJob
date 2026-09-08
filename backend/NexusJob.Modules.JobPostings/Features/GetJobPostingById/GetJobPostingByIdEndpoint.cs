using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.JobPostings.Features.GetJobPostingById;

/// <summary>
/// The <c>GET /api/job-postings/{id}</c> delegate; it only calls the slice
/// handler (AD-14). Anonymous (AD-18): no auth filter, no antiforgery. The
/// <c>{id:guid}</c> route constraint makes a non-GUID segment a routing 404
/// before this runs.
/// </summary>
internal static class GetJobPostingByIdEndpoint
{
    public static Task<IResult> Handle(
        [FromRoute] Guid id,
        [FromServices] GetJobPostingByIdHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(id, cancellationToken);
}
