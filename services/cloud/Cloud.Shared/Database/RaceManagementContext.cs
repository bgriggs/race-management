using Cloud.Shared.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Cloud.Shared.Database;

public class RaceManagementContext(DbContextOptions<RaceManagementContext> options) : DbContext(options)
{
    public DbSet<Team> Teams { get; set; } = null!;
    public DbSet<Car> Cars { get; set; } = null!;
    public DbSet<UserRoleMapping> UserRoleMappings { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (!optionsBuilder.IsConfigured)
        {
            // Enable legacy timestamp behavior BEFORE configuring Npgsql
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Team>()
            .HasIndex(t => t.ClientId);

        modelBuilder.Entity<Car>()
            .HasOne<Team>()
            .WithMany()
            .HasForeignKey(u => u.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRoleMapping>()
            .HasOne<Team>()
            .WithMany()
            .HasForeignKey(u => u.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}