using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models;

[PrimaryKey(nameof(TeamId), nameof(Number))]
public class Car
{
    public int TeamId { get; set; }
    [StringLength(6, MinimumLength = 1)]
    public required string Number { get; set; }
    [StringLength(20)]
    public required string Make { get; set; }
    [StringLength(20)]
    public required string Model { get; set; }
    [StringLength(20)]
    public required string Color { get; set; }
    public bool IsDeleted { get; set; }
}
