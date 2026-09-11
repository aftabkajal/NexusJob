using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusJob.Modules.Applications.Auth;
using NexusJob.Modules.Applications.Features.CreateApplication;
using NexusJob.Modules.Applications.Features.GetMyApplication;
using NexusJob.Modules.Applications.Features.GetMyApplicationsList;
using NexusJob.Modules.Applications.Persistence;
using Npgsql;

namespace NexusJob.Modules.Applications;

/// <summary>
/// Composition entry points for the Applications module. The Host is the only
/// caller (AD-10). <see cref="AddApplicationsModule"/> registers only
/// Applications' own services - its <see cref="ApplicationsDbContext"/> and the
/// slice handlers / endpoint filters. The app-wide auth infrastructure (cookie
/// scheme, antiforgery, Data-Protection, startup migration) lives in the Host
/// (AD-13 / AD-22).
/// </summary>
public static class ApplicationsModule
{
    private const string ConnectionName = "Postgres";

    /// <summary>Registers the Applications module's services.</summary>
    public static IServiceCollection AddApplicationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = BuildApplicationsConnectionString(configuration.GetConnectionString(ConnectionName));

        services.AddDbContext<ApplicationsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications")));

        services.AddScoped<AntiforgeryEndpointFilter>();
        services.AddScoped<JobSeekerOnlyEndpointFilter>();
        services.AddScoped<CreateApplicationHandler>();
        services.AddScoped<GetMyApplicationHandler>();
        services.AddScoped<GetMyApplicationsListHandler>();

        return services;
    }

    /// <summary>Maps the <c>/api/applications</c> endpoint group (AD-4 owns these routes).</summary>
    public static IEndpointRouteBuilder MapApplicationsModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/applications");

        // Operation ids drive the generated TypeScript client (AD-15): the
        // "Applications_*" prefix makes NSwag emit one `ApplicationsClient` with
        // `create` / `getMine` methods.
        //
        // POST filter order mirrors POST /api/job-postings: RequireAuthorization
        // -> 401 for anonymous; JobSeekerOnly -> 403 for a signed-in non-Job
        // Seeker; antiforgery -> 400 for a missing/invalid X-CSRF-TOKEN;
        // DataAnnotations -> 400 for an empty/invalid body. The handler runs only
        // when all four pass.
        group.MapPost("", CreateApplicationEndpoint.Handle)
            .WithName("Applications_Create")
            .RequireAuthorization()
            .AddEndpointFilter<JobSeekerOnlyEndpointFilter>()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .AddEndpointFilter(new DataAnnotationsValidationFilter<CreateApplicationRequest>())
            .Accepts<CreateApplicationRequest>("application/json")
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /mine is a Job Seeker read: RequireAuthorization + JobSeekerOnly
        // only, no antiforgery on a GET. Missing / non-GUID jobPostingId is a
        // 400 from the handler (spec I/O matrix).
        group.MapGet("/mine", GetMyApplicationEndpoint.Handle)
            .WithName("Applications_GetMine")
            .RequireAuthorization()
            .AddEndpointFilter<JobSeekerOnlyEndpointFilter>()
            .Produces<MyApplicationResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // GET /mine/list is a sibling route of /mine (spec Boundaries &
        // Constraints, Decision 2026-09-12) - a new operation id, not a second
        // shape on Applications_GetMine. A Job Seeker read: RequireAuthorization
        // + JobSeekerOnly only, no antiforgery on a GET.
        group.MapGet("/mine/list", GetMyApplicationsListEndpoint.Handle)
            .WithName("Applications_GetMyApplications")
            .RequireAuthorization()
            .AddEndpointFilter<JobSeekerOnlyEndpointFilter>()
            .Produces<Page<MyApplicationListItemResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    /// <summary>
    /// Returns the Postgres connection string with <c>SearchPath=applications</c>
    /// forced on (AD-5). When nothing is configured, or the value cannot be
    /// parsed, a non-functional placeholder is used so an unconfigured Host still
    /// boots - <c>/health</c> and the startup migration then report the failure.
    /// Mirrors <c>JobPostingsModule.BuildJobPostingsConnectionString</c>.
    /// </summary>
    private static string BuildApplicationsConnectionString(string? raw)
    {
        var baseConnectionString = string.IsNullOrWhiteSpace(raw)
            ? "Host=localhost;Database=nexusjob;Username=nexusjob;Password=unconfigured_placeholder;Timeout=3;Command Timeout=3"
            : raw;

        try
        {
            return new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = "applications" }.ConnectionString;
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            return baseConnectionString;
        }
    }
}

/// <summary>
/// Assembly marker for <c>NexusJob.Modules.Applications</c>. Lets the architecture
/// tests load this implementation assembly without a project reference.
/// </summary>
public sealed class ApplicationsModuleAssembly;
