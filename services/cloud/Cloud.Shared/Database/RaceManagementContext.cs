using Cloud.Shared.Database.Models;
using Cloud.Shared.Database.Models.Alarms;
using Cloud.Shared.Database.Models.FuelAnalysis;
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

    public DbSet<RefuelEvent> RefuelEvents { get; set; } = null!;
    public DbSet<FuelWindow> FuelWindows { get; set; } = null!;
    public DbSet<Stint> Stints { get; set; } = null!;
    public DbSet<CalibrationFactor> CalibrationFactors { get; set; } = null!;
    public DbSet<TeamFuelDefaults> TeamFuelDefaults { get; set; } = null!;

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

        modelBuilder.Entity<Race>(e =>
        {
            e.HasOne<Team>()
                .WithMany()
                .HasForeignKey(r => r.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            // Race.Start is a naive wall-clock — engineers enter and read it
            // without any TZ adjustment. Stored as `timestamp without time zone`
            // so Npgsql preserves DateTimeKind.Unspecified across the round-trip
            // (Npgsql 6+ requires Kind.Utc for `timestamptz` and Kind.Unspecified
            // for `timestamp`).
            e.Property(r => r.Start).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<SiteSettings>()
            .HasOne<Team>()
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        ConfigureFuelAnalysis(modelBuilder);
        ConfigureAlarms(modelBuilder);
    }

    private static void ConfigureFuelAnalysis(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefuelEvent>(e =>
        {
            e.HasOne<Car>().WithMany().HasForeignKey(r => new { r.TeamId, r.CarNumber }).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Race>().WithMany().HasForeignKey(r => r.RaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.TeamId, r.CarNumber, r.RaceId, r.DetectedAt });
            e.Property(r => r.ConfidenceTier).HasConversion<string>().HasMaxLength(16);
            e.Property(r => r.Source).HasConversion<string>().HasMaxLength(32);
            e.Property(r => r.EcuResetState).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<FuelWindow>(e =>
        {
            e.HasOne<Car>().WithMany().HasForeignKey(w => new { w.TeamId, w.CarNumber }).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Race>().WithMany().HasForeignKey(w => w.RaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<RefuelEvent>().WithMany().HasForeignKey(w => w.StartRefuelEventId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<RefuelEvent>().WithMany().HasForeignKey(w => w.EndRefuelEventId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(w => new { w.TeamId, w.CarNumber, w.RaceId, w.OpenedAt });
            // Partial index for the at-most-one open window per car
            e.HasIndex(w => new { w.TeamId, w.CarNumber, w.RaceId })
                .HasFilter("\"ClosedAt\" IS NULL")
                .IsUnique();
        });

        modelBuilder.Entity<Stint>(e =>
        {
            e.HasOne<Car>().WithMany().HasForeignKey(s => new { s.TeamId, s.CarNumber }).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Race>().WithMany().HasForeignKey(s => s.RaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<FuelWindow>().WithMany().HasForeignKey(s => s.FuelWindowId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.TeamId, s.CarNumber, s.RaceId, s.StartAt });
            e.HasIndex(s => s.FuelWindowId);
            e.Property(s => s.OriginType).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<CalibrationFactor>(e =>
        {
            e.HasOne<Car>().WithMany().HasForeignKey(c => new { c.TeamId, c.CarNumber }).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Race>().WithMany().HasForeignKey(c => c.RaceId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(c => new { c.TeamId, c.CarNumber, c.EffectiveAt });
            e.Property(c => c.Source).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<TeamFuelDefaults>()
            .HasOne<Team>()
            .WithMany()
            .HasForeignKey(d => d.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
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
