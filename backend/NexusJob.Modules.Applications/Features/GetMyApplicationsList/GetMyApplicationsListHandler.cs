using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Applications.Persistence;
using NexusJob.Modules.JobPostings.Contracts;

namespace NexusJob.Modules.Applications.Features.GetMyApplicationsList;

/// <summary>
/// Returns the calling Job Seeker's applications as a paged list, most-recent
/// first (story 3.3a). The caller is resolved from the
/// <see cref="ClaimTypes.NameIdentifier"/> claim only (AD-13);
/// <c>RequireAuthorization()</c> plus the
/// <see cref="Auth.JobSeekerOnlyEndpointFilter"/> already guarantee an
/// authenticated Job Seeker principal, so the unparseable-id branch is purely
/// defensive.
///
/// Clamp constants and arithmetic are copied verbatim from
/// <c>JobPostings.Features.SearchJobPostings.SearchJobPostingsHandler</c>
/// (AD-15): <c>page</c> 1-based, <c>pageSize</c> default 20 / max 100, and a
/// <see langword="long"/>-arithmetic, overflow-safe <c>Skip</c>.
///
/// Posting titles are resolved through exactly one batched
/// <see cref="IJobPostingsApi.GetPostingSummaries"/> call for the whole page
/// (AD-5 / AD-19) - never a per-row lookup. A row whose posting is
/// unexpectedly absent from that batch (a data-integrity violation,
/// unreachable in v1 - nothing deletes a posting) is dropped from the page
/// rather than failing the whole request, mirroring
/// <c>SearchJobPostingsHandler</c>'s orphan-owner handling (spec Design Notes).
/// <c>total</c> is the raw count of the caller's applications, computed once
/// before any batch resolution or row-dropping.
/// </summary>
internal sealed class GetMyApplicationsListHandler(ApplicationsDbContext db, IJobPostingsApi jobPostingsApi)
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<IResult> HandleAsync(
        int? page,
        int? pageSize,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var nameIdentifier = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(nameIdentifier, out var jobSeekerId))
        {
            return Results.Problem(
                title: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var clampedPage = page is null or < 1 ? 1 : page.Value;
        var clampedPageSize = pageSize is null or < 1 or > MaxPageSize ? DefaultPageSize : pageSize.Value;

        var matches = db.Applications.Where(a => a.JobSeekerId == jobSeekerId);

        var total = await matches.CountAsync(cancellationToken);

        // long arithmetic, clamped to int.MaxValue - see
        // SearchJobPostingsHandler for the overflow rationale copied verbatim.
        var skip = (long)(clampedPage - 1) * clampedPageSize;
        var clampedSkip = skip > int.MaxValue ? int.MaxValue : (int)skip;

        var rows = await matches
            .OrderByDescending(a => a.SubmittedAt)
            .ThenBy(a => a.Id)
            .Skip(clampedSkip)
            .Take(clampedPageSize)
            .Select(a => new { a.Id, a.JobPostingId, a.SubmittedAt })
            .ToListAsync(cancellationToken);

        var postingIds = rows.Select(r => r.JobPostingId).Distinct().ToArray();
        var postings = jobPostingsApi.GetPostingSummaries(postingIds);

        var items = new List<MyApplicationListItemResponse>(rows.Count);
        foreach (var row in rows)
        {
            if (!postings.TryGetValue(row.JobPostingId, out var posting))
            {
                // Data-integrity violation, unreachable in v1 (nothing deletes
                // a posting): drop the row rather than fail the whole page
                // (spec Design Notes).
                continue;
            }

            items.Add(new MyApplicationListItemResponse(
                row.Id.ToString(),
                row.JobPostingId.ToString(),
                posting.Title,
                row.SubmittedAt));
        }

        return Results.Ok(new Page<MyApplicationListItemResponse>(items, clampedPage, clampedPageSize, total));
    }
}
