using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Applications.Persistence;

namespace NexusJob.Modules.Applications.Features.GetMyApplication;

/// <summary>
/// Reports whether the calling Job Seeker has applied to a given posting (AD-20) -
/// the on-load apply-button state. The caller is resolved from the
/// <see cref="ClaimTypes.NameIdentifier"/> claim only (AD-13);
/// <c>RequireAuthorization()</c> plus the
/// <see cref="Auth.JobSeekerOnlyEndpointFilter"/> already guarantee an
/// authenticated Job Seeker principal, so the unparseable-id branch is purely
/// defensive.
///
/// A single indexed read against the <c>applications</c> schema (AD-8). This
/// endpoint does <em>not</em> check that the posting exists (spec Design Notes):
/// a bogus or deleted <c>jobPostingId</c> the caller never applied to is
/// indistinguishable from a real one they have not applied to - both correctly
/// yield <c>{ applied: false, appliedAt: null }</c>.
/// </summary>
internal sealed class GetMyApplicationHandler(ApplicationsDbContext db)
{
    public async Task<IResult> HandleAsync(
        string? jobPostingId,
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

        if (!Guid.TryParse(jobPostingId, out var postingId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["JobPostingId"] = ["A valid job posting id is required."],
            });
        }

        var submittedAt = await db.Applications
            .Where(a => a.JobPostingId == postingId && a.JobSeekerId == jobSeekerId)
            .Select(a => (DateTimeOffset?)a.SubmittedAt)
            .SingleOrDefaultAsync(cancellationToken);

        return Results.Ok(new MyApplicationResponse(submittedAt is not null, submittedAt));
    }
}
