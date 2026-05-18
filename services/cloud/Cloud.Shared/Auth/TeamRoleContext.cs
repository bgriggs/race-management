using System.Security.Claims;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Cloud.Shared.Auth;

public interface ITeamRoleContext
{
    /// <summary>Returns every <see cref="UserRoleMapping"/> for the caller (resolved from the JWT email claim).</summary>
    Task<IReadOnlyList<UserRoleMapping>> GetUserTeamsAsync(CancellationToken ct = default);

    /// <summary>Returns every <see cref="UserRoleMapping"/> for the given <paramref name="email"/>. Used by callers (e.g. SignalR hubs) that resolve the principal themselves.</summary>
    Task<IReadOnlyList<UserRoleMapping>> GetUserTeamsAsync(string email, CancellationToken ct = default);

    /// <summary>True when the caller is a member of <paramref name="teamId"/>.</summary>
    Task<bool> IsUserInTeamAsync(int teamId, CancellationToken ct = default);

    /// <summary>True when the user identified by <paramref name="email"/> is a member of <paramref name="teamId"/>.</summary>
    Task<bool> IsUserInTeamAsync(string email, int teamId, CancellationToken ct = default);

    /// <summary>True when the caller has <paramref name="role"/> within <paramref name="teamId"/>.</summary>
    Task<bool> IsUserInTeamRoleAsync(int teamId, string role, CancellationToken ct = default);
}

public class TeamRoleContext(
    IHttpContextAccessor httpContextAccessor,
    IDbContextFactory<RaceManagementContext> db,
    HybridCache hcache) : ITeamRoleContext
{
    private static readonly IReadOnlyList<UserRoleMapping> _empty = [];

    public Task<IReadOnlyList<UserRoleMapping>> GetUserTeamsAsync(CancellationToken ct = default)
    {
        var email = GetEmail();
        return string.IsNullOrEmpty(email)
            ? Task.FromResult(_empty)
            : GetUserTeamsAsync(email, ct);
    }

    public async Task<IReadOnlyList<UserRoleMapping>> GetUserTeamsAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email)) return _empty;

        return await hcache.GetOrCreateAsync(
            $"user-teams:{email}",
            async cancel =>
            {
                await using var context = await db.CreateDbContextAsync(cancel);
                var rows = await context.UserRoleMappings
                    .AsNoTracking()
                    .Where(m => m.Email == email)
                    .ToListAsync(cancel);
                return (IReadOnlyList<UserRoleMapping>)rows;
            },
            cancellationToken: ct);
    }

    public async Task<bool> IsUserInTeamAsync(int teamId, CancellationToken ct = default)
    {
        var teams = await GetUserTeamsAsync(ct);
        foreach (var mapping in teams)
        {
            if (mapping.TeamId == teamId) return true;
        }
        return false;
    }

    public async Task<bool> IsUserInTeamAsync(string email, int teamId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(email)) return false;
        var teams = await GetUserTeamsAsync(email, ct);
        foreach (var mapping in teams)
        {
            if (mapping.TeamId == teamId) return true;
        }
        return false;
    }

    public async Task<bool> IsUserInTeamRoleAsync(int teamId, string role, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(role)) return false;
        var teams = await GetUserTeamsAsync(ct);
        foreach (var mapping in teams)
        {
            if (mapping.TeamId == teamId && HasRole(mapping.Roles, role)) return true;
        }
        return false;
    }

    private string? GetEmail()
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(ClaimTypes.Email)?.Value
            ?? user?.FindFirst("email")?.Value;
    }

    private static bool HasRole(string roles, string role)
    {
        foreach (var part in roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.Equals(part, role, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

public static class TeamRoleContextExtensions
{
    /// <summary>
    /// Registers <see cref="ITeamRoleContext"/> and <see cref="IHttpContextAccessor"/>.
    /// The caller must register <c>HybridCache</c> separately (e.g. <c>services.AddHybridCache(...)</c>).
    /// </summary>
    public static IServiceCollection AddTeamRoleContext(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ITeamRoleContext, TeamRoleContext>();
        return services;
    }
}
