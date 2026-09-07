using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NexusJob.Modules.JobPostings.Features.CreateJobPosting;

/// <summary>The <c>POST /api/job-postings</c> delegate; it only calls the slice handler (AD-14).</summary>
internal static class CreateJobPostingEndpoint
{
    public static Task<IResult> Handle(
        [FromBody] CreateJobPostingRequest request,
        [FromServices] CreateJobPostingHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(request, httpContext, cancellationToken);
}
