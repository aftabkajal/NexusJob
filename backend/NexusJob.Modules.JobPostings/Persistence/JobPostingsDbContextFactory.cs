using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NexusJob.Modules.JobPostings.Persistence;

/// <summary>
/// Design-time factory used only by <c>dotnet ef migrations</c>. It never
/// connects to a database (migration scaffolding is offline); the placeholder
/// connection string only has to parse. Runtime wiring is in
/// <see cref="JobPostingsModule.AddJobPostingsModule"/>.
/// </summary>
internal sealed class JobPostingsDbContextFactory : IDesignTimeDbContextFactory<JobPostingsDbContext>
{
    public JobPostingsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<JobPostingsDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=nexusjob;Username=nexusjob;Password=design_time_placeholder;SearchPath=job_postings",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "job_postings"))
            .Options;

        return new JobPostingsDbContext(options);
    }
}
