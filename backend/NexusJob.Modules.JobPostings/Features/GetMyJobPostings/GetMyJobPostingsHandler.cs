using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.JobPostings.Features.SearchJobPostings;
using NexusJob.Modules.JobPostings.Persistence;

namespace NexusJob.Modules.JobPostings.Features.GetMyJobPostings;

/// <summary>
/// Returns the calling Company's own postings as a paged list, most-recent first
/// (spec 3.4a). The caller is resolved from the <see cref="ClaimTypes.NameIdentifier"/>
/// claim only (AD-13); <c>RequireAuthorization()</c> plus
/// <see cref="Auth.CompanyOnlyEndpointFilter"/> already guarantee an
/// authenticated Company principal, so the unparseable-id branch is purely
/// defensive.
///
/// Clamp constants and arithmetic are copied verbatim from
/// <see cref="SearchJobPostingsHandler"/> (AD-15): <c>page</c> 1-based,
/// <c>pageSize</c> default 20 / max 100, and a <see langword="long"/>-arithmetic,
/// overflow-safe <c>Skip</c>. Reuses <see cref="SearchJobPostings.Page{T}"/> -
/// the module's own existing page shape, reachable from this sibling feature
/// folder in the same assembly - rather than a third copy (spec Design Notes).
///
/// No new index: the existing <c>IX_job_posting_owner_company_id</c> index
/// already makes <c>owner_company_id</c> this query's leading, indexed column
/// (spec Boundaries &amp; Constraints).
/// </summary>
internal sealed class GetMyJobPostingsHandler(JobPostingsDbContext db)
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
        if (!Guid.TryParse(nameIdentifier, out var ownerCompanyId))
        {
            return Results.Problem(
                title: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var clampedPage = page is null or < 1 ? 1 : page.Value;
        var clampedPageSize = pageSize is null or < 1 or > MaxPageSize ? DefaultPageSize : pageSize.Value;

        var matches = db.JobPostings.Where(p => p.OwnerCompanyId == ownerCompanyId);

        var total = await matches.CountAsync(cancellationToken);

        // long arithmetic, clamped to int.MaxValue - see SearchJobPostingsHandler
        // for the overflow rationale copied verbatim.
        var skip = (long)(clampedPage - 1) * clampedPageSize;
        var clampedSkip = skip > int.MaxValue ? int.MaxValue : (int)skip;

        var items = await matches
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Skip(clampedSkip)
            .Take(clampedPageSize)
            .Select(p => new JobPostingMineItemResponse(
                p.Id.ToString(),
                p.Title,
                p.Description,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(new Page<JobPostingMineItemResponse>(items, clampedPage, clampedPageSize, total));
    }
}
