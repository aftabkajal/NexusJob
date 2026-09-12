using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Applications.Features.GetMyApplicationsList;
using NexusJob.Modules.Applications.Persistence;
using NexusJob.Modules.Identity.Contracts;
using NexusJob.Modules.JobPostings.Contracts;

namespace NexusJob.Modules.Applications.Features.GetApplicants;

/// <summary>
/// Returns the paged list of applicants for a posting the calling Company owns,
/// most-recent first (FR-7 / spec 3.4a). The caller is resolved from the
/// <see cref="ClaimTypes.NameIdentifier"/> claim only (AD-13);
/// <c>RequireAuthorization()</c> plus <see cref="Auth.CompanyOnlyEndpointFilter"/>
/// already guarantee an authenticated Company principal, so the unparseable-id
/// branch is purely defensive.
///
/// <b>Ownership check (AD-9):</b> <see cref="IJobPostingsApi.GetPostingOwner"/>
/// returning <see langword="null"/> (no such posting) or a non-null owner that
/// does not equal the caller's id (someone else's posting) both take the same
/// branch to the same <c>404</c> <see cref="Results.Problem"/> call - one
/// <c>if</c>, one <c>return</c>, so the two responses are byte-identical by
/// construction, never a distinct <c>403</c> for "not yours".
///
/// Clamp constants and arithmetic are copied verbatim from
/// <c>JobPostings.Features.SearchJobPostings.SearchJobPostingsHandler</c>
/// (AD-15). Applicant identity is resolved through exactly one batched
/// <see cref="IIdentityApi.GetJobSeekers"/> call for the whole page (AD-5 /
/// AD-19) - never a per-row lookup. A row whose Job Seeker is unexpectedly
/// absent from that batch (a data-integrity violation, unreachable in v1 -
/// nothing deletes a Job Seeker account) is dropped from the page rather than
/// failing the whole request, mirroring <c>SearchJobPostingsHandler</c>'s /
/// <c>GetMyApplicationsListHandler</c>'s orphan-row handling (spec Design
/// Notes). <c>total</c> is the raw match count, computed once before any batch
/// resolution or row-dropping.
///
/// Reuses <see cref="GetMyApplicationsList.Page{T}"/> - the module's own
/// existing page shape, reachable from this sibling feature folder in the same
/// assembly - rather than a third copy (spec Design Notes).
/// </summary>
internal sealed class GetApplicantsHandler(
    ApplicationsDbContext db,
    IJobPostingsApi jobPostingsApi,
    IIdentityApi identityApi)
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<IResult> HandleAsync(
        string? jobPostingId,
        int? page,
        int? pageSize,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var nameIdentifier = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(nameIdentifier, out var companyId))
        {
            return Results.Problem(
                title: "Authentication is required.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!Guid.TryParse(jobPostingId, out var postingId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["JobPostingId"] = ["A valid job posting id is required."],
            });
        }

        var owner = jobPostingsApi.GetPostingOwner(postingId);
        if (owner is null || owner != companyId)
        {
            return Results.Problem(
                title: "This posting is no longer available.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var clampedPage = page is null or < 1 ? 1 : page.Value;
        var clampedPageSize = pageSize is null or < 1 or > MaxPageSize ? DefaultPageSize : pageSize.Value;

        var matches = db.Applications.Where(a => a.JobPostingId == postingId);

        var total = await matches.CountAsync(cancellationToken);

        // long arithmetic, clamped to int.MaxValue - see SearchJobPostingsHandler
        // for the overflow rationale copied verbatim.
        var skip = (long)(clampedPage - 1) * clampedPageSize;
        var clampedSkip = skip > int.MaxValue ? int.MaxValue : (int)skip;

        var rows = await matches
            .OrderByDescending(a => a.SubmittedAt)
            .ThenBy(a => a.Id)
            .Skip(clampedSkip)
            .Take(clampedPageSize)
            .Select(a => new { a.JobSeekerId, a.SubmittedAt })
            .ToListAsync(cancellationToken);

        var seekerIds = rows.Select(r => r.JobSeekerId).Distinct().ToArray();
        var jobSeekers = identityApi.GetJobSeekers(seekerIds);

        var items = new List<ApplicantListItemResponse>(rows.Count);
        foreach (var row in rows)
        {
            if (!jobSeekers.TryGetValue(row.JobSeekerId, out var jobSeeker))
            {
                // Data-integrity violation, unreachable in v1 (nothing deletes a
                // Job Seeker account): drop the row rather than fail the whole
                // page (spec Design Notes).
                continue;
            }

            items.Add(new ApplicantListItemResponse(
                row.JobSeekerId.ToString(),
                jobSeeker.FullName,
                jobSeeker.Email,
                row.SubmittedAt));
        }

        return Results.Ok(new Page<ApplicantListItemResponse>(items, clampedPage, clampedPageSize, total));
    }
}
