using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models;

[PrimaryKey(nameof(TeamId), nameof(UserId))]
public class ChannelStatusTableConfiguration
{
    public int TeamId { get; set; }

    [MaxLength(128)]
    public required string UserId { get; set; }

    public List<ChannelStatusTableColumnConfiguration> Columns { get; set; } = [];
}

[PrimaryKey(nameof(TeamId), nameof(UserId), nameof(ChannelDefinitionId))]
public class ChannelStatusTableColumnConfiguration
{
    public int TeamId { get; set; }

    [MaxLength(128)]
    public string UserId { get; set; } = string.Empty;

    public Guid ChannelDefinitionId { get; set; }
    public int Order { get; set; }
    public string? NameOverride { get; set; }
}
