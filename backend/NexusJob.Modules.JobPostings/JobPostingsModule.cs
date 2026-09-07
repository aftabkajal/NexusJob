using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusJob.Modules.JobPostings.Auth;
using NexusJob.Modules.JobPostings.Features.CreateJobPosting;
using NexusJob.Modules.JobPostings.Persistence;
using Npgsql;

namespace NexusJob.Modules.JobPostings;

/// <summary>
/// Composition entry points for the JobPostings module. The Host is the only
/// caller (AD-10). <see cref="AddJobPostingsModule"/> registers only JobPostings'
/// own services - its <see cref="JobPostingsDbContext"/> and the slice handler /
/// endpoint filters. The app-wide auth infrastructure (cookie scheme,
/// antiforgery, Data-Protection, startup migration) lives in the Host
/// (AD-13 / AD-22).
/// </summary>
public static class JobPostingsModule
{
    private const string ConnectionName = "Postgres";

    /// <summary>Registers the JobPostings module's services.</summary>
    public static IServiceCollection AddJobPostingsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = BuildJobPostingsConnectionString(configuration.GetConnectionString(ConnectionName));

        services.AddDbContext<JobPostingsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "job_postings")));

        services.AddScoped<AntiforgeryEndpointFilter>();
        services.AddScoped<CompanyOnlyEndpointFilter>();
        services.AddScoped<CreateJobPostingHandler>();

        return services;
    }

    /// <summary>Maps the <c>/api/job-postings</c> endpoint group (AD-4 owns these routes).</summary>
    public static IEndpointRouteBuilder MapJobPostingsModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/job-postings");

        // Operation id drives the generated TypeScript client (AD-15): the
        // "JobPostings_*" prefix makes NSwag emit one `JobPostingsClient` with a
        // `create` method. Filter order: RequireAuthorization -> 401 for
        // anonymous; CompanyOnly -> 403 for a signed-in non-Company; antiforgery
        // -> 400 for a missing/invalid X-CSRF-TOKEN; DataAnnotations -> 400 for an
        // empty/invalid body. The handler runs only when all four pass.
        group.MapPost("", CreateJobPostingEndpoint.Handle)
            .WithName("JobPostings_Create")
            .RequireAuthorization()
            .AddEndpointFilter<CompanyOnlyEndpointFilter>()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .AddEndpointFilter(new DataAnnotationsValidationFilter<CreateJobPostingRequest>())
            .Accepts<CreateJobPostingRequest>("application/json")
            .Produces<JobPostingResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    /// <summary>
    /// Returns the Postgres connection string with <c>SearchPath=job_postings</c>
    /// forced on (AD-5). When nothing is configured, or the value cannot be
    /// parsed, a non-functional placeholder is used so an unconfigured Host still
    /// boots - <c>/health</c> and the startup migration then report the failure.
    /// Mirrors <c>IdentityModule.BuildIdentityConnectionString</c>.
    /// </summary>
    private static string BuildJobPostingsConnectionString(string? raw)
    {
        var baseConnectionString = string.IsNullOrWhiteSpace(raw)
            ? "Host=localhost;Database=nexusjob;Username=nexusjob;Password=unconfigured_placeholder;Timeout=3;Command Timeout=3"
            : raw;

        try
        {
            return new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = "job_postings" }.ConnectionString;
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            return baseConnectionString;
        }
    }
}

/// <summary>
/// Assembly marker for <c>NexusJob.Modules.JobPostings</c>. Lets the architecture
/// tests load this implementation assembly without a project reference.
/// </summary>
public sealed class JobPostingsModuleAssembly;
