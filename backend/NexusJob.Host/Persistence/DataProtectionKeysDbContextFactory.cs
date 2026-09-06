using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NexusJob.Host.Persistence;

/// <summary>
/// Design-time factory used only by <c>dotnet ef migrations</c>, so the tooling
/// does not have to boot the whole Host pipeline. It never connects (scaffolding
/// is offline); the placeholder connection string only has to parse. Runtime
/// wiring is in <c>Program.cs</c>.
/// </summary>
internal sealed class DataProtectionKeysDbContextFactory : IDesignTimeDbContextFactory<DataProtectionKeysDbContext>
{
    public DataProtectionKeysDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DataProtectionKeysDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=nexusjob;Username=nexusjob;Password=design_time_placeholder",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;

        return new DataProtectionKeysDbContext(options);
    }
}
