using Microsoft.EntityFrameworkCore;

namespace Cloud.Shared.Database;

public class RaceManagementContext(DbContextOptions<RaceManagementContext> options) : DbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (!optionsBuilder.IsConfigured)
        {
            // Enable legacy timestamp behavior BEFORE configuring Npgsql
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }
    }
}