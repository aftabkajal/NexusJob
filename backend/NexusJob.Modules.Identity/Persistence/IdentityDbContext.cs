using Microsoft.EntityFrameworkCore;

namespace NexusJob.Modules.Identity.Persistence;

/// <summary>
/// The Identity module's own <see cref="DbContext"/> (AD-5): it maps only
/// Identity's entities and its default schema is <c>identity</c>. Identity owns
/// its own EF migration history (<c>identity.__EFMigrationsHistory</c>, AD-7).
///
/// Public only so the composition root (the Host) can run
/// <c>Database.Migrate()</c> for it at startup (AD-10 / Resolved Decisions). No
/// other project can reference this assembly (AD-2), so the surface stays
/// effectively module-private.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    internal DbSet<CompanyAccount> CompanyAccounts => Set<CompanyAccount>();

    internal DbSet<JobSeekerAccount> JobSeekerAccounts => Set<JobSeekerAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<CompanyAccount>(entity =>
        {
            entity.ToTable("company_account");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("display_name").IsRequired();

            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<JobSeekerAccount>(entity =>
        {
            entity.ToTable("job_seeker_account");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email").IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.FullName).HasColumnName("full_name").IsRequired();

            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}
