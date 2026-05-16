using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models;

[PrimaryKey(nameof(TeamId), nameof(Email))]
public class UserRoleMapping
{
    public int TeamId { get; set; }
    [StringLength(256, MinimumLength = 5)]
    public required string Email { get; set; }
    [StringLength(256)]
    public required string Roles { get; set; }
}
