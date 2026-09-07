using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Identity.Auth;
using NexusJob.Modules.Identity.Features.Register;
using NexusJob.Modules.Identity.Persistence;

namespace NexusJob.Modules.Identity.Features.GetMe;

/// <summary>
/// Returns the signed-in account's summary (auth endpoints beyond register/login,
/// epic context). The account <em>id</em> is resolved from
/// <see cref="ClaimTypes.NameIdentifier"/> only (AD-13); the
/// <c>account_type</c> claim - set by <see cref="ClaimsPrincipalFactory"/> at
/// sign-in - only chooses which table to read, since a Guid v7 is not guaranteed
/// unique across the two independent tables (spec Design Notes). An absent or
/// unrecognised <c>account_type</c> claim yields <c>401</c>. <c>displayName</c> is
/// read fresh so a later name change shows without re-issuing the cookie.
/// </summary>
internal sealed class GetMeHandler(IdentityDbContext db)
{
    public async Task<IResult> HandleAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var nameIdentifier = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(nameIdentifier, out var accountId))
        {
            return Unauthorized();
        }

        var accountType = AccountType.Classify(httpContext.User.FindFirstValue(AccountType.ClaimType));

        var summary = accountType switch
        {
            AccountType.Company => await db.CompanyAccounts
                .Where(a => a.Id == accountId)
                .Select(a => new AuthAccountResponse(a.Id.ToString(), AccountType.Company, a.DisplayName))
                .SingleOrDefaultAsync(cancellationToken),
            AccountType.JobSeeker => await db.JobSeekerAccounts
                .Where(a => a.Id == accountId)
                .Select(a => new AuthAccountResponse(a.Id.ToString(), AccountType.JobSeeker, a.FullName))
                .SingleOrDefaultAsync(cancellationToken),
            _ => null,
        };

        return summary is null ? Unauthorized() : Results.Ok(summary);
    }

    private static IResult Unauthorized() => Results.Problem(
        title: "Authentication is required.",
        statusCode: StatusCodes.Status401Unauthorized);
}
