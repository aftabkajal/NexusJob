using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using NexusJob.Host.Persistence;
using NexusJob.Modules.Applications;
using NexusJob.Modules.Identity;
using NexusJob.Modules.Identity.Auth;
using NexusJob.Modules.Identity.Persistence;
using NexusJob.Modules.JobPostings;
using NexusJob.Modules.JobPostings.Persistence;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// The SPA build is copied into wwwroot by the Dockerfile. Guarantee the folder
// exists so UseStaticFiles / UseDefaultFiles work even on a fresh checkout with
// no frontend build. Resolve it against the host content root - the same root
// static-file serving uses - not the process CWD - and re-point the web-root
// file provider, since it may already have been resolved as "missing".
var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);
builder.Environment.WebRootPath = webRootPath;
builder.Environment.WebRootFileProvider = new PhysicalFileProvider(webRootPath);

// Operability floor (AD-22): structured JSON logs to stdout, and nothing else.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = false;
    options.UseUtcTimestamp = true;
    options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
});

// One structured line per handled request. Method, path, protocol, status and
// duration only - never headers or bodies, so no Set-Cookie / Authorization /
// password / hash value can reach the log (AD-13, AD-22).
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.RequestProtocol
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

// RFC 9457 ProblemDetails on every non-2xx (AD-15).
builder.Services.AddProblemDetails();

// One OpenAPI document (AD-15), pinned to 3.0 output. The .NET 10 generator
// defaults to 3.1; NSwag's TypeScript client generator is unreliable on 3.1, so
// both the served endpoint and the build-time file are forced to 3.0. The
// build-time file is written by Microsoft.Extensions.ApiDescription.Server (see
// the Host .csproj) and must stay byte-identical to what MapOpenApi serves.
builder.Services.AddOpenApi("v1", options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
});

// Built-in .NET 10 minimal-API validation (DataAnnotations). A request body that
// fails validation is a 400 validation ProblemDetails naming the invalid fields.
builder.Services.AddValidation();

// DB connection string comes only from environment / .NET configuration
// (key ConnectionStrings:Postgres, env ConnectionStrings__Postgres). Nothing
// secret is committed (AD-22).
var rawPostgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
var healthProbeConnectionString = HealthProbe.BuildProbeConnectionString(rawPostgresConnectionString);

// ---- App-wide auth infrastructure (AD-13, AD-22) -------------------------
// One cookie scheme for the whole app. Identity's /api/auth/* slice is the only
// code that signs it in / clears it; the Host only registers the plumbing.
builder.Services
    .AddAuthentication(AuthCookie.Scheme)
    .AddCookie(AuthCookie.Scheme, options =>
    {
        options.Cookie.Name = AuthCookie.CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);

        // Never redirect an API caller to an HTML login/access-denied page -
        // answer with a JSON ProblemDetails status (AD-15).
        options.Events.OnRedirectToLogin = context => WriteProblem(context, StatusCodes.Status401Unauthorized, "Authentication is required.");
        options.Events.OnRedirectToAccessDenied = context => WriteProblem(context, StatusCodes.Status403Forbidden, "Access is denied.");

        static Task WriteProblem(RedirectContext<CookieAuthenticationOptions> context, int statusCode, string title) =>
            Results.Problem(title: title, statusCode: statusCode).ExecuteAsync(context.HttpContext);
    });

builder.Services.AddAuthorization();

// Antiforgery double-submit: token in the X-CSRF-TOKEN header, seeded by
// GET /api/auth/csrf (AD-13).
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// Data-Protection keys persist to public.data_protection_keys, owned by the Host
// (AD-22), so the auth cookie survives restarts.
builder.Services.AddDbContext<DataProtectionKeysDbContext>(options =>
    options.UseNpgsql(
        DbConnectionStrings.ForDataProtection(rawPostgresConnectionString),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")));
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<DataProtectionKeysDbContext>();

// Module wiring (AD-10): the Host calls all three pairs and nothing else
// module-specific.
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddJobPostingsModule(builder.Configuration);
builder.Services.AddApplicationsModule(builder.Configuration);

var app = builder.Build();

// Unconditional startup migration (Resolved Decisions): apply IdentityDbContext
// then DataProtectionKeysDbContext before the app serves traffic. The whole
// block is wrapped in try/catch so the Host still starts when the database is
// down or misconfigured - GET /health reports "unhealthy" until a later restart
// applies the migrations. This preserves the story-1.1 invariant and keeps the
// existing HealthEndpointTests green.
using (var migrationScope = app.Services.CreateScope())
{
    try
    {
        migrationScope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.Migrate();
        migrationScope.ServiceProvider.GetRequiredService<DataProtectionKeysDbContext>().Database.Migrate();
        migrationScope.ServiceProvider.GetRequiredService<JobPostingsDbContext>().Database.Migrate();
    }
    catch (Exception migrationException)
    {
        app.Logger.LogError(
            migrationException,
            "Startup database migration failed. The Host will start; GET /health will report \"unhealthy\" "
            + "until the database is reachable and migrations apply on a later restart.");
    }
}

// Fail loudly (once, at startup) when the DB is not configured or its connection
// string cannot be parsed, so an unconfigured / misconfigured deployment is not
// indistinguishable from a database outage. Neither case takes the Host down -
// GET /health reports "unhealthy" until it is fixed.
if (string.IsNullOrWhiteSpace(rawPostgresConnectionString))
{
    app.Logger.LogError(
        "ConnectionStrings:Postgres is not configured. GET /health will report "
        + "\"unhealthy\" until the ConnectionStrings__Postgres environment variable is set.");
}
else if (healthProbeConnectionString is null)
{
    app.Logger.LogError(
        "ConnectionStrings:Postgres is set but could not be parsed. GET /health will "
        + "report \"unhealthy\" until the ConnectionStrings__Postgres value is corrected.");
}

app.UseHttpLogging();

// App-wide auth middleware (AD-13), after request logging and before the module
// endpoint maps. UseAntiforgery must sit after routing + authentication.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Same-origin static SPA hosting (AD-12): no CORS, no proxy.
app.UseDefaultFiles();
app.UseStaticFiles();

// Operability floor (AD-22): liveness + a real SELECT 1 against Postgres, with a
// bounded connect + command timeout so a listening-but-stalled server cannot
// hang the probe.
app.MapGet("/health", async (CancellationToken requestAborted) =>
{
    if (healthProbeConnectionString is null)
    {
        return Results.Json(
            new HealthResponse("unhealthy", "unreachable"),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var connection = new NpgsqlConnection(healthProbeConnectionString);
        await connection.OpenAsync(requestAborted);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        _ = await command.ExecuteScalarAsync(requestAborted);

        return Results.Json(
            new HealthResponse("healthy", "ok"),
            statusCode: StatusCodes.Status200OK);
    }
    catch (Exception e) when (e is not OperationCanceledException)
    {
        // Probe failure (unreachable / refused / timed out) maps to 503 with a
        // fixed body; the exception and any connection detail are never leaked.
        // An OperationCanceledException means the caller disconnected - let it
        // propagate rather than reporting a false DB outage.
        return Results.Json(
            new HealthResponse("unhealthy", "unreachable"),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// The OpenAPI 3.0 document at /openapi/v1.json (AD-15): anonymous (explicitly,
// not just by the absence of a fallback policy), same-origin, and mapped ahead
// of the SPA fallback below so MapFallbackToFile never shadows it. JSON endpoint
// only - no Swagger UI middleware.
app.MapOpenApi().AllowAnonymous();

// Module endpoints (AD-10). No-op stubs in story 1.1.
app.MapIdentityModule();
app.MapJobPostingsModule();
app.MapApplicationsModule();

// An unknown /api/* route is a 404, never the SPA shell - a client expecting
// JSON must not get index.html with a 200. This fallback is more specific than
// the catch-all below, so a matched API endpoint still wins and any other
// unmatched /api path lands here.
app.MapFallback("/api/{**rest}", () => Results.NotFound());

// Any non-API, non-file route returns the SPA shell from the same origin.
// Registered after the API routes so it never shadows them.
app.MapFallbackToFile("index.html");

app.Run();

/// <summary>Body shape for <c>GET /health</c>. Serialized camelCase: <c>status</c>, <c>database</c>.</summary>
internal sealed record HealthResponse(string Status, string Database);

/// <summary>Helpers for the <c>/health</c> DB probe.</summary>
internal static class HealthProbe
{
    /// <summary>Connect timeout for the probe, in seconds.</summary>
    private const int ConnectTimeoutSeconds = 3;

    /// <summary>Command timeout for the <c>SELECT 1</c>, in seconds.</summary>
    private const int CommandTimeoutSeconds = 3;

    /// <summary>
    /// Returns <paramref name="connectionString"/> with short connect and command
    /// timeouts forced on, so the probe fails fast instead of blocking ~15-30s on
    /// a stalled server. Returns <c>null</c> when nothing is configured, or when
    /// the value is present but not a parseable Npgsql connection string - a
    /// syntactically invalid value must not crash the Host at startup.
    /// </summary>
    internal static string? BuildProbeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = ConnectTimeoutSeconds,
                CommandTimeout = CommandTimeoutSeconds,
            }.ConnectionString;
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            return null;
        }
    }
}

/// <summary>Connection-string helpers for the Host-owned EF contexts.</summary>
internal static class DbConnectionStrings
{
    /// <summary>
    /// The Postgres connection string for the Data-Protection key context. When
    /// nothing is configured a non-functional placeholder is returned so the Host
    /// still boots (the startup migration then logs the failure and <c>/health</c>
    /// reports "unhealthy"); a malformed value is handed through unchanged for the
    /// same reason. No <c>SearchPath</c> override - the table lives in <c>public</c>.
    /// </summary>
    internal static string ForDataProtection(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? "Host=localhost;Database=nexusjob;Username=nexusjob;Password=unconfigured_placeholder;Timeout=3;Command Timeout=3"
            : raw;
}

/// <summary>Public entry-point marker so WebApplicationFactory-style tests can target this assembly.</summary>
public partial class Program;
