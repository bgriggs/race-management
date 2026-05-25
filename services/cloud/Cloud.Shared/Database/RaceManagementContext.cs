using Cloud.Shared.Database.Models;
using Cloud.Shared.Database.Models.Alarms;
using Microsoft.EntityFrameworkCore;

namespace Cloud.Shared.Database;

public class RaceManagementContext(DbContextOptions<RaceManagementContext> options) : DbContext(options)
{
    public DbSet<Team> Teams { get; set; } = null!;
    public DbSet<Car> Cars { get; set; } = null!;
    public DbSet<UserRoleMapping> UserRoleMappings { get; set; } = null!;
    public DbSet<CarConfigurationTable> CarConfigurations { get; set; } = null!;
    public DbSet<ChannelStatusTableConfiguration> ChannelStatusTableConfigurations { get; set; } = null!;
    public DbSet<Race> Races { get; set; } = null!;
    public DbSet<SiteSettings> SiteSettings { get; set; } = null!;

    public DbSet<AlarmDefinitionRow> AlarmDefinitions { get; set; } = null!;
    public DbSet<ActiveAlarmRow> ActiveAlarms { get; set; } = null!;
    public DbSet<AlarmEventRow> AlarmEvents { get; set; } = null!;


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

        modelBuilder.Entity<CarConfigurationTable>()
            .HasOne<Team>()
            .WithMany()
            .HasForeignKey(u => u.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CarConfigurationTable>()
            .HasIndex(c => new { c.TeamId, c.Car });

        modelBuilder.Entity<ChannelStatusTableConfiguration>()
            .HasOne<Team>()
            .WithMany()
            .HasForeignKey(u => u.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChannelStatusTableConfiguration>()
            .HasIndex(c => new { c.TeamId, c.UserId });

        modelBuilder.Entity<ChannelStatusTableConfiguration>()
            .HasMany(c => c.Columns)
            .WithOne()
            .HasForeignKey(c => new { c.TeamId, c.UserId })
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Race>()
            .HasOne<Team>()
            .WithMany()
            .HasForeignKey(r => r.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SiteSettings>()
            .HasOne<Team>()
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        ConfigureAlarms(modelBuilder);
    }

    private static void ConfigureAlarms(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlarmDefinitionRow>(e =>
        {
            e.HasOne<Team>().WithMany().HasForeignKey(a => a.TeamId).OnDelete(DeleteBehavior.Cascade);
            // Conditional FK to Car only when CarNumber is set (team-level rows skip the FK).
            // EF Core cannot model a partial-nullable composite FK declaratively; the
            // application enforces that CarNumber, when set, refers to an existing car.
            e.Property(a => a.StatementJson).HasColumnType("jsonb");
            e.HasIndex(a => new { a.TeamId, a.CarNumber });
        });

        modelBuilder.Entity<ActiveAlarmRow>(e =>
        {
            e.HasOne<Car>().WithMany().HasForeignKey(a => new { a.TeamId, a.CarNumber }).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AlarmDefinitionRow>().WithMany().HasForeignKey(a => a.AlarmDefinitionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.TeamId, a.IsActive });
        });

        modelBuilder.Entity<AlarmEventRow>(e =>
        {
            e.HasOne<Car>().WithMany().HasForeignKey(a => new { a.TeamId, a.CarNumber }).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AlarmDefinitionRow>().WithMany().HasForeignKey(a => a.AlarmDefinitionId).OnDelete(DeleteBehavior.Cascade);
            e.Property(a => a.EventType).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(a => new { a.TeamId, a.CarNumber, a.AlarmDefinitionId, a.Timestamp });
        });
    }
}