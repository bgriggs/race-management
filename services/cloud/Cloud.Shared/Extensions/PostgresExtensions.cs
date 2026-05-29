using Cloud.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cloud.Shared.Extensions;

public static class PostgresExtensions
{
    /// <summary>
    /// Registers a <see cref="RaceManagementContext"/> factory and a Postgres health check.
    /// Reads the connection string from <c>ConnectionStrings:Default</c> in configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="minPoolSize">Minimum number of connections in the pool. Defaults to 1.</param>
    /// <param name="maxPoolSize">Maximum number of connections in the pool. Defaults to 5.</param>
    public static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration,
        int minPoolSize = 2, int maxPoolSize = 10)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        string sqlConn = configuration["ConnectionStrings:Default"] ?? throw new ArgumentNullException("SQL Connection");
        var npgsqlConnBuilder = new NpgsqlConnectionStringBuilder(sqlConn) { MinPoolSize = minPoolSize, MaxPoolSize = maxPoolSize };
        services.AddDbContextFactory<RaceManagementContext>(op => op.UseNpgsql(npgsqlConnBuilder.ConnectionString));
        services.AddHealthChecks()
            .AddNpgSql(sqlConn, name: "postgres", tags: ["db", "postgres"]);

        return services;
    }
}
