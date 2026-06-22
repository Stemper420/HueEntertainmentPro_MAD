using HueArtNet.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace HueArtNet.Persistence;

public sealed class HueArtNetDbContext : DbContext
{
  public HueArtNetDbContext(DbContextOptions<HueArtNetDbContext> options)
    : base(options)
  {
  }

  public DbSet<ShowProfileEntity> ShowProfiles => Set<ShowProfileEntity>();
  public DbSet<HubMappingEntity> HubMappings => Set<HubMappingEntity>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<ShowProfileEntity>(entity =>
    {
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Name).IsRequired();
      entity.HasMany(x => x.HubMappings)
        .WithOne(x => x.ShowProfile)
        .HasForeignKey(x => x.ShowProfileId)
        .OnDelete(DeleteBehavior.Cascade);
    });

    modelBuilder.Entity<HubMappingEntity>(entity =>
    {
      entity.HasKey(x => x.Id);
      entity.Property(x => x.Name).IsRequired();
      entity.Property(x => x.HueBridgeId).IsRequired();
      entity.Property(x => x.BridgeIp).IsRequired();
      entity.Property(x => x.ProtectedApplicationKey).IsRequired();
      entity.Property(x => x.ProtectedEntertainmentKey).IsRequired();
      entity.Property(x => x.FixtureMode).IsRequired();
      entity.Property(x => x.LightOrderJson).IsRequired();
    });
  }
}
