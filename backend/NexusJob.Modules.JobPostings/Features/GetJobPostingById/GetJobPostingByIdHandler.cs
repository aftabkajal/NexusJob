using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Identity.Contracts;
using NexusJob.Modules.JobPostings.Persistence;

namespace NexusJob.Modules.JobPostings.Features.GetJobPostingById;

/// <summary>
/// Reads a job posting's detail (FR-5). Projects the posting from the
/// <c>job_postings</c> schema, then resolves the owning Company's display name
/// through <see cref="IIdentityApi"/> only (AD-5 / AD-19) - never a query against
/// the <c>identity</c> schema. A single in-process <see cref="IIdentityApi.GetCompany"/>
/// call (one posting, one owner - no N+1; this is a detail read, not a list).
///
/// A well-formed id with no matching row is an RFC 9457 <c>404</c> (AD-15). An
/// existing posting whose owner Company cannot be resolved is a data-integrity
/// violation (nothing deletes a Company in v1) and surfaces as an RFC 9457
/// <c>500</c> rather than a masked <c>404</c> or an invented name (spec Design
/// Notes, 2.1a precedent).
/// </summary>
internal sealed class GetJobPostingByIdHandler(JobPostingsDbContext db, IIdentityApi identityApi)
{
    public async Task<IResult> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.JobPostings
            .Where(p => p.Id == id)
            .Select(p => new { p.Id, p.Title, p.Description, p.OwnerCompanyId })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return Results.Problem(
                title: "This posting is no longer available.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var company = identityApi.GetCompany(row.OwnerCompanyId);
        if (company is null)
        {
            // Data-integrity violation: a posting always carries an owner id taken
            // from an authenticated Company at create time, and nothing deletes a
            // Company in v1. Surface it as an RFC 9457 500 - never a masked 404 or
            // an invented name (spec Design Notes, 2.1a precedent).
            return Results.Problem(
                title: "The job posting could not be loaded.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.Ok(new JobPostingDetailResponse(
            row.Id.ToString(),
            row.Title,
            row.Description,
            company.DisplayName));
    }
}
