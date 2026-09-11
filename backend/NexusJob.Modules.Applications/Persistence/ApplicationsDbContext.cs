using Microsoft.EntityFrameworkCore;

namespace NexusJob.Modules.Applications.Persistence;

/// <summary>
/// The Applications module's own <see cref="DbContext"/> (AD-5): it maps only
/// Applications' entities and its default schema is <c>applications</c>.
/// Applications owns its own EF migration history
/// (<c>applications.__EFMigrationsHistory</c>, AD-6 / AD-7).
///
/// Public only so the composition root (the Host) can run
/// <c>Database.Migrate()</c> for it at startup (AD-10). No other project can
/// reference this assembly (AD-2), so the surface stays effectively
/// module-private - the <see cref="Applications"/> set and the
/// <see cref="Application"/> entity are <c>internal</c>.
/// </summary>
public sealed class ApplicationsDbContext(DbContextOptions<ApplicationsDbContext> options) : DbContext(options)
{
    internal DbSet<Application> Applications => Set<Application>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("applications");

        modelBuilder.Entity<Application>(entity =>
        {
            entity.ToTable("application");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.JobPostingId).HasColumnName("job_posting_id");
            entity.Property(e => e.JobSeekerId).HasColumnName("job_seeker_id");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");

            // The one-application-per-pair rule (AD-4). This unique constraint is
            // the idempotency guard (AD-9 / AD-20): the apply handler attempts the
            // insert and catches the 23505 unique-violation - no pre-check SELECT.
            entity.HasIndex(e => new { e.JobPostingId, e.JobSeekerId }).IsUnique();

            // Serves GetMyApplicationsListHandler's `WHERE job_seeker_id = @seeker
            // ORDER BY submitted_at DESC` query (story 3.3a). The unique pair
            // index above has job_posting_id as its leading column, so it cannot
            // serve this access pattern efficiently.
            entity.HasIndex(e => new { e.JobSeekerId, e.SubmittedAt });
        });
    }
}
