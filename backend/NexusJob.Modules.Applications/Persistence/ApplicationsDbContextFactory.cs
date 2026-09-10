using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NexusJob.Modules.Applications.Persistence;

/// <summary>
/// Design-time factory used only by <c>dotnet ef migrations</c>. It never
/// connects to a database (migration scaffolding is offline); the placeholder
/// connection string only has to parse. Runtime wiring is in
/// <see cref="ApplicationsModule.AddApplicationsModule"/>.
/// </summary>
internal sealed class ApplicationsDbContextFactory : IDesignTimeDbContextFactory<ApplicationsDbContext>
{
    public ApplicationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=nexusjob;Username=nexusjob;Password=design_time_placeholder;SearchPath=applications",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications"))
            .Options;

        return new ApplicationsDbContext(options);
    }
}
