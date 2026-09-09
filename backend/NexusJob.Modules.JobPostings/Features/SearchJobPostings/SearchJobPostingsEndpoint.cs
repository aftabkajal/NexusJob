using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.JobPostings.Features.SearchJobPostings;

/// <summary>
/// The <c>GET /api/job-postings</c> delegate; it only calls the slice handler
/// (AD-14). Anonymous (AD-18): no auth filter, no antiforgery. <c>query</c>,
/// <c>page</c>, and <c>pageSize</c> are all optional - a missing/empty
/// <c>query</c> means "browse everything" (spec Intent).
/// </summary>
internal static class SearchJobPostingsEndpoint
{
    public static Task<IResult> Handle(
        [FromQuery] string? query,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] SearchJobPostingsHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(query, page, pageSize, cancellationToken);
}
