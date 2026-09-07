using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NexusJob.Modules.JobPostings.Persistence;

namespace NexusJob.Modules.JobPostings.Features.CreateJobPosting;

/// <summary>
/// Creates a job posting (FR-3). The owning Company is resolved from the caller's
/// <see cref="ClaimTypes.NameIdentifier"/> claim only (AD-13);
/// <c>RequireAuthorization()</c> plus the
/// <see cref="Auth.CompanyOnlyEndpointFilter"/> already guarantee an authenticated
/// Company principal, so the unparseable-id branch is purely defensive.
///
/// One module, one <c>SaveChanges</c> against the <c>job_postings</c> schema
/// (AD-8). The <c>id</c> is a <see cref="Guid"/> v7 generated here;
/// <c>created_at</c> is the current UTC instant. Success is <c>200</c> with the
/// representation (no envelope, no <c>Location</c>) - consistent with Identity's
/// register (spec Resolved Decisions).
/// </summary>
internal sealed class CreateJobPostingHandler(JobPostingsDbContext db)
{
    public async Task<IResult> HandleAsync(
        CreateJobPostingRequest request,
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

        var posting = new JobPosting
        {
            Id = Guid.CreateVersion7(),
            OwnerCompanyId = ownerCompanyId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.JobPostings.Add(posting);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new JobPostingResponse(
            posting.Id.ToString(),
            posting.Title,
            posting.Description,
            posting.CreatedAt));
    }
}
