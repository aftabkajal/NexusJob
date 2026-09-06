namespace NexusJob.Modules.Identity.Features;

/// <summary>
/// The signed-in account summary returned by <c>POST /api/auth/register</c>,
/// <c>POST /api/auth/login</c> and <c>GET /api/auth/me</c> - one shape, no
/// envelope (AD-15). Serialised as <c>{ id, accountType, displayName }</c>;
/// <c>id</c> is the account <see cref="System.Guid"/> as a string (AD-15
/// conventions).
/// </summary>
public sealed record AuthAccountResponse(string Id, string AccountType, string DisplayName);
