using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace NexusJob.IntegrationTests;

/// <summary>
/// Boots a real Postgres (Testcontainers, <c>postgres:18</c>) once for the whole
/// <see cref="AuthEndpointsTests"/> class and hosts the app against it through
/// <see cref="WebApplicationFactory{TEntryPoint}"/>. The factory boot runs the
/// Host's unconditional startup migration, so both <c>IdentityDbContext</c> and
/// <c>DataProtectionKeysDbContext</c> are migrated against the container before
/// the first request (I/O-matrix row "Host starts with the database reachable").
/// The <c>PasswordHasher</c> iteration count is dialed down to keep PBKDF2 from
/// dominating the suite (spec Design Notes); production still reads >= 600000.
/// </summary>
public sealed class IdentityApiFixture : IAsyncLifetime
{
    private const int TestIterationCount = 10_000;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    internal LogSink Logs { get; } = new();

    internal string ConnectionString { get; private set; } = string.Empty;

    internal WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("Fixture not initialized.");

    /// <summary>
    /// The hosted app's root <see cref="IServiceProvider"/> - so a contract test
    /// can resolve <c>IIdentityApi</c> / <c>IJobPostingsApi</c> from a scope
    /// (spec 2.2a <c>ContractApiTests</c>).
    /// </summary>
    internal IServiceProvider Services => Factory.Services;

    /// <summary>
    /// A cookie-handling client whose base address is <c>https://localhost</c>, so
    /// the <c>Secure</c> auth cookie (AD-13) is stored and replayed by the in-memory
    /// test server exactly as a browser would over TLS.
    /// </summary>
    internal HttpClient CreateClient() => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", ConnectionString);
            builder.UseSetting("Auth:PasswordHasher:IterationCount", TestIterationCount.ToString());
            builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(Logs)));
        });

        // Force the host (and its startup migration) to build now.
        _ = _factory.Services.GetService<IServiceProvider>();
        using var warmup = CreateClient();
        using (await warmup.GetAsync("/health"))
        {
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}
