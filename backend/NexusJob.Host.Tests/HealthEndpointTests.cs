using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NexusJob.Host.Tests;

/// <summary>
/// The DB-down path of the operability floor (AD-22 / I/O matrix row "Health, DB
/// down"). A DB-up integration test is deferred (Testcontainers, later story).
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
}
