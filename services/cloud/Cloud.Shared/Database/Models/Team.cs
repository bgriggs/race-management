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
}
