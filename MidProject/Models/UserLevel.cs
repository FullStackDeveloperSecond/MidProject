using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class UserLevel
{
    public int LevelID { get; set; }

    [Required, StringLength(50)]
    public string LevelName { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int MinExp { get; set; }

    [StringLength(50)]
    public string? Rewards { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
