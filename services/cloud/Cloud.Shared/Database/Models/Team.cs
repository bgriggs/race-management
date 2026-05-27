using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models;

public class Team
{
    public int Id { get; set; }

    [StringLength(20, MinimumLength = 1)]
    public required string Name { get; set; }

    [StringLength(20, MinimumLength = 3)]
    public required string ClientId { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>
    /// The Race the team is currently monitoring — drives the ChannelProcessor's RedMist
    /// subscription. When non-null, the activation evaluator picks this Race directly,
    /// bypassing the time-window rule (the user explicitly chose). When null, the
    /// evaluator falls back to the time-window auto-pick. Shared across all engineers
    /// connected to the team; updated via the Race-Header dropdown.
    /// </summary>
    public int? SelectedRaceId { get; set; }
}
