using Cloud.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Cloud.Tests.ChannelProcessor.RedMist;

/// <summary>
/// Minimal <see cref="IDbContextFactory{TContext}"/> over a pre-built <see cref="DbContextOptions{TContext}"/>.
/// Each <c>CreateDbContext</c> call returns a fresh context bound to the same in-memory
/// database name, so the SUT's <c>await using var db = await dbFactory.CreateDbContextAsync()</c>
/// pattern works the way it does in production.
/// </summary>
internal sealed class TestDbContextFactory(DbContextOptions<RaceManagementContext> options)
    : IDbContextFactory<RaceManagementContext>
{
    public RaceManagementContext CreateDbContext() => new(options);
}
