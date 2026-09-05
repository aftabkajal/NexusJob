using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NexusJob.Modules.JobPostings;

/// <summary>
/// Composition entry points for the JobPostings module. The Host is the only
/// caller (AD-10). Story 1.1 keeps both methods as no-op stubs.
/// </summary>
public static class JobPostingsModule
{
    /// <summary>Registers the JobPostings module's services. No-op stub for story 1.1.</summary>
    public static IServiceCollection AddJobPostingsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    /// <summary>Maps the JobPostings module's endpoints. No-op stub for story 1.1.</summary>
    public static IEndpointRouteBuilder MapJobPostingsModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }
}

/// <summary>
/// Assembly marker for <c>NexusJob.Modules.JobPostings</c>. Lets the
/// architecture tests load this implementation assembly without a project
/// reference.
/// </summary>
public sealed class JobPostingsModuleAssembly;
