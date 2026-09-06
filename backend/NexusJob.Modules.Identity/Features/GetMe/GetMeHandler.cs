using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NexusJob.Modules.Identity.Auth;
using NexusJob.Modules.Identity.Features.Register;
using NexusJob.Modules.Identity.Persistence;

namespace NexusJob.Modules.Identity.Features.GetMe;

/// <summary>
/// Returns the signed-in Company's summary (auth endpoints beyond register/login,
/// epic context). Identity is resolved from <see cref="ClaimTypes.NameIdentifier"/>
/// only (AD-13); <c>displayName</c> is read fresh from <c>company_account</c> so a
/// later name change shows without re-issuing the cookie (spec Design Notes).
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

        var summary = await db.CompanyAccounts
            .Where(a => a.Id == accountId)
            .Select(a => new AuthAccountResponse(a.Id.ToString(), AccountType.Company, a.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);

        return summary is null ? Unauthorized() : Results.Ok(summary);
    }

    private static IResult Unauthorized() => Results.Problem(
        title: "Authentication is required.",
        statusCode: StatusCodes.Status401Unauthorized);
}
