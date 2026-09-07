using Microsoft.EntityFrameworkCore;

namespace NexusJob.Modules.JobPostings.Persistence;

/// <summary>
/// The JobPostings module's own <see cref="DbContext"/> (AD-5): it maps only
/// JobPostings' entities and its default schema is <c>job_postings</c>.
/// JobPostings owns its own EF migration history
/// (<c>job_postings.__EFMigrationsHistory</c>, AD-6 / AD-7).
///
/// Public only so the composition root (the Host) can run
/// <c>Database.Migrate()</c> for it at startup (AD-10 / Resolved Decisions). No
/// other project can reference this assembly (AD-2), so the surface stays
/// effectively module-private - the <see cref="JobPostings"/> set and the
/// <see cref="JobPosting"/> entity are <c>internal</c>.
/// </summary>
public sealed class JobPostingsDbContext(DbContextOptions<JobPostingsDbContext> options) : DbContext(options)
{
    internal DbSet<JobPosting> JobPostings => Set<JobPosting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("job_postings");

        modelBuilder.Entity<JobPosting>(entity =>
        {
            entity.ToTable("job_posting");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OwnerCompanyId).HasColumnName("owner_company_id");
            entity.Property(e => e.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnName("description").IsRequired().HasMaxLength(4000);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasIndex(e => e.OwnerCompanyId);
        });
    }
}
