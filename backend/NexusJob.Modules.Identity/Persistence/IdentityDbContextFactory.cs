using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NexusJob.Modules.Identity.Persistence;

/// <summary>
/// Design-time factory used only by <c>dotnet ef migrations</c>. It never
/// connects to a database (migration scaffolding is offline); the placeholder
/// connection string only has to parse. Runtime wiring is in
/// <see cref="IdentityModule.AddIdentityModule"/>.
/// </summary>
internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=nexusjob;Username=nexusjob;Password=design_time_placeholder;SearchPath=identity",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"))
            .Options;

        return new IdentityDbContext(options);
    }
}
