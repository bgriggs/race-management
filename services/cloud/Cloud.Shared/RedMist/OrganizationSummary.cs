namespace Cloud.Shared.RedMist;

/// <summary>
/// Wire format for one RedMist organization as returned by
/// <c>GET /v1/Organizations/GetOrganizations</c> on the RedMist Status API.
/// Mirrors RedMist's server-side <c>OrganizationSummary</c> record exactly.
/// </summary>
public sealed record OrganizationSummary(int Id, string Name, string? Website);
