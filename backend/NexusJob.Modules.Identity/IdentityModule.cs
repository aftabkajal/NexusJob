using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NexusJob.Modules.Identity;

/// <summary>
/// Composition entry points for the Identity module. The Host is the only
/// caller (AD-10). Story 1.1 keeps both methods as no-op stubs: services,
/// endpoints, persistence, and auth arrive in stories 1.3 / 1.4.
/// </summary>
public static class IdentityModule
{
    /// <summary>Registers the Identity module's services. No-op stub for story 1.1.</summary>
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    /// <summary>Maps the Identity module's endpoints. No-op stub for story 1.1.</summary>
    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }
}

/// <summary>
/// Assembly marker for <c>NexusJob.Modules.Identity</c>. Lets the architecture
/// tests load this implementation assembly without taking a project reference
/// on it (which would itself violate AD-2 rule 3).
/// </summary>
public sealed class IdentityModuleAssembly;
