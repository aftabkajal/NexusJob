using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.FileProviders;
using NexusJob.Modules.Applications;
using NexusJob.Modules.Identity;
using NexusJob.Modules.JobPostings;
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

// DB connection string comes only from environment / .NET configuration
// (key ConnectionStrings:Postgres, env ConnectionStrings__Postgres). Nothing
// secret is committed (AD-22).
var rawPostgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
var healthProbeConnectionString = HealthProbe.BuildProbeConnectionString(rawPostgresConnectionString);

// Module wiring (AD-10): the Host calls all three pairs and nothing else
// module-specific.
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddJobPostingsModule(builder.Configuration);
builder.Services.AddApplicationsModule(builder.Configuration);

var app = builder.Build();

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

/// <summary>Public entry-point marker so WebApplicationFactory-style tests can target this assembly.</summary>
public partial class Program;
