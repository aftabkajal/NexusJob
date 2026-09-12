using Microsoft.AspNetCore.Http;

namespace NexusJob.Modules.Applications.Auth;

/// <summary>
/// Restricts an endpoint to authenticated <b>Company</b> callers (AD-13 /
/// AD-18). <c>RequireAuthorization()</c> runs first and yields <c>401</c> for a
/// missing or invalid cookie; this filter then runs and yields <c>403</c> when
/// the authenticated principal's <c>account_type</c> claim is not
/// <c>company</c> - so a genuinely signed-in Job Seeker is <em>forbidden</em>,
/// not <em>unauthenticated</em>.
///
/// The claim name and value are the cookie-auth wire contract (AD-13); this
/// module hardcodes the two strings rather than referencing Identity's
/// <c>internal AccountType</c> type (AD-1). A verbatim per-module copy of
/// <c>NexusJob.Modules.JobPostings.Auth.CompanyOnlyEndpointFilter</c>, mirroring
/// how <c>JobSeekerOnlyEndpointFilter</c> was already duplicated the other
/// direction in 3.1a (spec 3.4a).
/// </summary>
internal sealed class CompanyOnlyEndpointFilter : IEndpointFilter
{
    /// <summary>The claim type carrying the account type on the auth cookie (AD-13).</summary>
    internal const string AccountTypeClaim = "account_type";

    /// <summary>The <c>account_type</c> claim value for a Company account.</summary>
    internal const string CompanyAccountType = "company";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var accountType = context.HttpContext.User.FindFirst(AccountTypeClaim)?.Value;

        if (!string.Equals(accountType, CompanyAccountType, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                title: "This action is available to Company accounts only.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
