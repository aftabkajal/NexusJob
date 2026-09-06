using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// I/O-matrix row "Host starts with the database down / misconfigured": the
/// unconditional startup migration fails, is caught and logged, and the process
/// does not exit - the Host still serves, with <c>/health</c> reporting <c>503</c>.
/// No container: the point is that an unreachable database does not stop boot.
/// </summary>
public sealed class HostStartupWithDatabaseDownTests
{
    [Fact]
    public async Task Host_still_serves_when_the_database_is_unreachable_and_health_is_503()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            // 127.0.0.1:1 refuses immediately - a deterministic "DB unreachable".
            builder.UseSetting(
                "ConnectionStrings:Postgres",
                "Host=127.0.0.1;Port=1;Database=nexusjob;Username=probe;Password=probe;Timeout=3;Command Timeout=3"));

        using var client = factory.CreateClient();

        // The startup migration threw, was caught and logged, and the process did
        // not exit: the Host is up and serving.
        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Equal(
            "{\"status\":\"unhealthy\",\"database\":\"unreachable\"}",
            await health.Content.ReadAsStringAsync());

        using var unknownApi = await client.GetAsync("/api/not-a-real-endpoint");
        Assert.Equal(HttpStatusCode.NotFound, unknownApi.StatusCode);
    }
}
