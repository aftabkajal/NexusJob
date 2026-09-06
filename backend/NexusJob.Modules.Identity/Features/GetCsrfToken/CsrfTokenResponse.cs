namespace NexusJob.Modules.Identity.Features.GetCsrfToken;

/// <summary>Body of <c>GET /api/auth/csrf</c>: <c>{ token }</c> - the value to echo in the <c>X-CSRF-TOKEN</c> header.</summary>
public sealed record CsrfTokenResponse(string Token);
