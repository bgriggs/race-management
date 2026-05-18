using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models;

[PrimaryKey(nameof(TeamId))]
public class SiteSettings
{
    public int TeamId { get; set; }
    [MaxLength(50)]
    public string RedMistClientId { get; set; } = string.Empty;
    [MaxLength(80)]
    public string RedMistClientSecret { get; set; } = string.Empty;
}
