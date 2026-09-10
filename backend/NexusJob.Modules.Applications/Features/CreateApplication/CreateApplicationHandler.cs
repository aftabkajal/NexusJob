using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Applications.Persistence;
using NexusJob.Modules.JobPostings.Contracts;
using Npgsql;

namespace NexusJob.Modules.Applications.Features.CreateApplication;

/// <summary>
/// Submits a Job Seeker's application to a posting (FR-6). The applicant is
/// resolved from the caller's <see cref="ClaimTypes.NameIdentifier"/> claim only
/// (AD-13); <c>RequireAuthorization()</c> plus the
/// <see cref="Auth.JobSeekerOnlyEndpointFilter"/> already guarantee an
/// authenticated Job Seeker principal, so the unparseable-id branch is purely
/// defensive.
///
/// The target posting's existence is checked through
/// <see cref="IJobPostingsApi.GetPostingOwner"/> only (AD-9 / AD-19) - never a
/// query against the <c>job_postings</c> schema; a <see langword="null"/> owner
/// yields <c>404</c>. The returned owner id carries no ownership restriction -
/// any Job Seeker may apply to any posting.
///
/// Idempotency is constraint-first (AD-9 / AD-20): the insert is attempted with
/// no guarding <c>SELECT</c>; on the Postgres unique-violation (SQLSTATE 23505)
/// the existing row is re-read and returned with <c>200</c> - never <c>409</c>,
/// never <c>500</c>. One module, one <c>SaveChanges</c> against the
/// <c>applications</c> schema (AD-8). Success is <c>200</c> with the
/// representation (no envelope, no <c>Location</c>).
/// </summary>
internal sealed class CreateApplicationHandler(ApplicationsDbContext db, IJobPostingsApi jobPostingsApi)
{
    public async Task<IResult> HandleAsync(
        CreateApplicationRequest request,
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

        if (!Guid.TryParse(request.JobPostingId, out var jobPostingId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["JobPostingId"] = ["A valid job posting id is required."],
            });
        }

        if (jobPostingsApi.GetPostingOwner(jobPostingId) is null)
        {
            return Results.Problem(
                title: "This posting is no longer available.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var application = new Application
        {
            Id = Guid.CreateVersion7(),
            JobPostingId = jobPostingId,
            JobSeekerId = jobSeekerId,
            SubmittedAt = DateTimeOffset.UtcNow,
        };

        db.Applications.Add(application);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A repeat or concurrent apply lost the race with the unique
            // constraint on (job_posting_id, job_seeker_id) (AD-9 / AD-20). The
            // constraint is the guard; no second row exists. Re-read the existing
            // row purely to build the 200 body. A caught 23505 whose follow-up
            // read finds nothing is impossible in v1 (nothing deletes an
            // application) - let SingleAsync surface that as a 500 (spec Design
            // Notes).
            db.ChangeTracker.Clear();
            var existing = await db.Applications
                .Where(a => a.JobPostingId == jobPostingId && a.JobSeekerId == jobSeekerId)
                .SingleAsync(cancellationToken);

            return Results.Ok(new ApplicationResponse(
                existing.Id.ToString(),
                existing.JobPostingId.ToString(),
                existing.SubmittedAt));
        }

        return Results.Ok(new ApplicationResponse(
            application.Id.ToString(),
            application.JobPostingId.ToString(),
            application.SubmittedAt));
    }
}
