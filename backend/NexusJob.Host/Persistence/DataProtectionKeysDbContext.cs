using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NexusJob.Host.Persistence;

/// <summary>
/// Host-owned store for ASP.NET Core Data-Protection keys (AD-22), so the auth
/// cookie survives restarts and multiple instances agree. The keys are
/// infrastructure, not domain data: the table lives in the <c>public</c> schema
/// and this context keeps its own migration history
/// (<c>public.__EFMigrationsHistory</c>), separate from every module's.
/// </summary>
public sealed class DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<DataProtectionKey>(entity =>
        {
            entity.ToTable("data_protection_keys");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FriendlyName).HasColumnName("friendly_name");
            entity.Property(e => e.Xml).HasColumnName("xml");
        });
    }
}
