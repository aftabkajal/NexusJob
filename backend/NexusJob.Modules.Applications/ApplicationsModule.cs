using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NexusJob.Modules.Applications;

/// <summary>
/// Composition entry points for the Applications module. The Host is the only
/// caller (AD-10). Story 1.1 keeps both methods as no-op stubs.
/// </summary>
public static class ApplicationsModule
{
    /// <summary>Registers the Applications module's services. No-op stub for story 1.1.</summary>
    public static IServiceCollection AddApplicationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    /// <summary>Maps the Applications module's endpoints. No-op stub for story 1.1.</summary>
    public static IEndpointRouteBuilder MapApplicationsModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }
}

/// <summary>
/// Assembly marker for <c>NexusJob.Modules.Applications</c>. Lets the
/// architecture tests load this implementation assembly without a project
/// reference.
/// </summary>
public sealed class ApplicationsModuleAssembly;
