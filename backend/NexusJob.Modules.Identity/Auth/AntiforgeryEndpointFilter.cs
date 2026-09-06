using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace NexusJob.Modules.Identity.Auth;

/// <summary>
/// Enforces the antiforgery double-submit check (AD-13) on the state-changing
/// <c>/api/auth/*</c> endpoints. The token is seeded by <c>GET /api/auth/csrf</c>
/// and sent back in the <c>X-CSRF-TOKEN</c> header. A missing or invalid token is
/// a <c>400</c> RFC 9457 ProblemDetails and the handler never runs, so there is
/// no state change (AD-15).
/// </summary>
internal sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                title: "Antiforgery token validation failed.",
                detail: "The required X-CSRF-TOKEN header is missing or invalid. Call GET /api/auth/csrf first.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
