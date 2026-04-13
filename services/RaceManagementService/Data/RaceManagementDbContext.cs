using Microsoft.EntityFrameworkCore;

namespace RaceManagementService.Data;

public class RaceManagementDbContext(DbContextOptions<RaceManagementDbContext> options) : DbContext(options)
{
    public DbSet<CarConfigurationEntity> CarConfigurations { get; set; }
}
