using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace NexusJob.Modules.Identity.Features.GetCsrfToken;

/// <summary>
/// Seeds the antiforgery double-submit pair (AD-13): <see cref="IAntiforgery.GetAndStoreTokens"/>
/// sets the antiforgery cookie on the response and returns the request token for
/// the client to send back in <c>X-CSRF-TOKEN</c>. Anonymous; no token required
/// to call it.
/// </summary>
internal sealed class GetCsrfTokenHandler(IAntiforgery antiforgery)
{
    public IResult Handle(HttpContext httpContext)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Ok(new CsrfTokenResponse(tokens.RequestToken ?? string.Empty));
    }
}
