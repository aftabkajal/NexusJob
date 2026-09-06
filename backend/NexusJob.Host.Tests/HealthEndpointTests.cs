using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NexusJob.Host.Tests;

/// <summary>
/// The DB-down / misconfigured paths of the operability floor (AD-22 / I/O matrix
/// row "Health, DB down"), plus the /api 404 contract. A DB-up integration test is
/// deferred (Testcontainers, later story).
/// </summary>
public sealed class HealthEndpointTests
{
    private sealed record HealthBody(string Status, string Database);

    [Fact]
    public async Task Health_returns_503_and_the_unhealthy_body_when_the_database_is_unreachable()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // 127.0.0.1:1 refuses immediately - a deterministic "DB unreachable".
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=127.0.0.1;Port=1;Database=nexusjob;Username=probe;Password=probe");
            });

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthBody>();
        Assert.NotNull(body);
        Assert.Equal("unhealthy", body!.Status);
        Assert.Equal("unreachable", body.Database);

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.Equal("{\"status\":\"unhealthy\",\"database\":\"unreachable\"}", rawBody);
    }

    [Fact]
    public async Task Health_returns_503_when_the_connection_string_is_present_but_malformed()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Syntactically invalid: a stray keyword Npgsql cannot parse. The
                // Host must still start and report "unhealthy" rather than crash.
                builder.UseSetting("ConnectionStrings:Postgres", "Host=db;NotAKeyword");
            });

        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.Equal("{\"status\":\"unhealthy\",\"database\":\"unreachable\"}", rawBody);
    }

    [Fact]
    public async Task Unknown_api_route_returns_404_and_not_the_spa_shell()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/not-a-real-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);
    }
}
