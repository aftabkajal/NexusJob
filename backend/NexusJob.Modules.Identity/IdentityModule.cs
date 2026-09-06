using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NexusJob.Modules.Identity.Auth;
using NexusJob.Modules.Identity.Features;
using NexusJob.Modules.Identity.Features.GetCsrfToken;
using NexusJob.Modules.Identity.Features.GetMe;
using NexusJob.Modules.Identity.Features.Login;
using NexusJob.Modules.Identity.Features.Logout;
using NexusJob.Modules.Identity.Features.Register;
using NexusJob.Modules.Identity.Persistence;
using Npgsql;

namespace NexusJob.Modules.Identity;

/// <summary>
/// Composition entry points for the Identity module. The Host is the only caller
/// (AD-10). <see cref="AddIdentityModule"/> registers only Identity's own
/// services - its <see cref="IdentityDbContext"/>, its <c>PasswordHasher</c>, and
/// the slice handlers. The app-wide auth infrastructure (cookie scheme,
/// antiforgery, Data-Protection, startup migration) lives in the Host
/// (AD-13 / AD-22 - see the spec Design Notes).
/// </summary>
public static class IdentityModule
{
    private const string ConnectionName = "Postgres";
    private const string IterationCountKey = "Auth:PasswordHasher:IterationCount";
    private const int DefaultIterationCount = 600_000;

    /// <summary>Registers the Identity module's services.</summary>
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = BuildIdentityConnectionString(configuration.GetConnectionString(ConnectionName));

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

        // PBKDF2-HMAC-SHA256 with an explicit iteration count from configuration
        // (AD-13). Never the library default; production reads >= 600000 from the
        // environment, tests inject a low value to keep the suite fast. A
        // non-integer value falls back to the default rather than throwing - an
        // Auth-config typo must not crash the Host at startup (it boots unhealthy,
        // like a bad connection string does).
        var iterationCount = int.TryParse(configuration[IterationCountKey], out var configuredIterationCount)
            ? configuredIterationCount
            : DefaultIterationCount;
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = iterationCount);
        services.AddScoped<IPasswordHasher<CompanyAccount>, PasswordHasher<CompanyAccount>>();

        services.AddScoped<AntiforgeryEndpointFilter>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<GetCsrfTokenHandler>();

        return services;
    }

    /// <summary>Maps the <c>/api/auth/*</c> endpoint group (AD-4 owns these routes).</summary>
    public static IEndpointRouteBuilder MapIdentityModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/auth");

        // Operation ids drive the generated TypeScript client (AD-15): the
        // "Auth_*" prefix makes NSwag emit one `AuthClient` with `register` /
        // `login` / `me` / `logout` / `csrf` methods. Endpoint behaviour is
        // unchanged - these only shape the document.
        group.MapPost("/register", RegisterEndpoint.Handle)
            .WithName("Auth_Register")
            .AllowAnonymous()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .AddEndpointFilter(new DataAnnotationsValidationFilter<RegisterRequest>())
            .Accepts<RegisterRequest>("application/json")
            .Produces<AuthAccountResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginEndpoint.Handle)
            .WithName("Auth_Login")
            .AllowAnonymous()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .AddEndpointFilter(new DataAnnotationsValidationFilter<LoginRequest>())
            .Accepts<LoginRequest>("application/json")
            .Produces<AuthAccountResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", GetMeEndpoint.Handle)
            .WithName("Auth_Me")
            .RequireAuthorization()
            .Produces<AuthAccountResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutEndpoint.Handle)
            .WithName("Auth_Logout")
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/csrf", GetCsrfTokenEndpoint.Handle)
            .WithName("Auth_Csrf")
            .AllowAnonymous()
            .Produces<CsrfTokenResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    /// <summary>
    /// Returns the Postgres connection string with <c>SearchPath=identity</c>
    /// forced on (AD-5). When nothing is configured, or the value cannot be
    /// parsed, a non-functional placeholder is used so an unconfigured Host still
    /// boots - <c>/health</c> and the startup migration then report the failure.
    /// </summary>
    private static string BuildIdentityConnectionString(string? raw)
    {
        var baseConnectionString = string.IsNullOrWhiteSpace(raw)
            ? "Host=localhost;Database=nexusjob;Username=nexusjob;Password=unconfigured_placeholder;Timeout=3;Command Timeout=3"
            : raw;

        try
        {
            return new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = "identity" }.ConnectionString;
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            return baseConnectionString;
        }
    }
}

/// <summary>
/// Assembly marker for <c>NexusJob.Modules.Identity</c>. Lets the architecture
/// tests load this implementation assembly without taking a project reference
/// on it (which would itself violate AD-2 rule 3).
/// </summary>
public sealed class IdentityModuleAssembly;
