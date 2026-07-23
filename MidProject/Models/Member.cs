using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MidProject.Models;

public class Member
{
    public int MemberID { get; set; }

    [Required, StringLength(50)]
    public string UserName { get; set; } = string.Empty;

    [StringLength(50)]
    public string? NickName { get; set; }

    [Required, StringLength(100), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [Required, StringLength(10)]
    public string Role { get; set; } = "User";

    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; }
    public bool IsDeleted { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Normal";

    // 記錄自動懲處機制上次套用時的受理檢舉次數，用來避免同一次數重複套用（見 AdminMembersController.ApplyAutoEscalationAsync）
    public int WarningCount { get; set; }

    public string? AdminNote { get; set; }
    public DateTime? PenaltyEndAt { get; set; }
    public DateOnly? Birthday { get; set; }

    public int LevelID { get; set; } = 1;
    public int Experience { get; set; }
    public int Points { get; set; }
    public int? AvatarImageID { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Image? AvatarImage { get; set; }
    public Member? DeletedByMember { get; set; }

    public ICollection<Restaurant> Restaurants { get; set; } = new List<Restaurant>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Image> UploadedImages { get; set; } = new List<Image>();
    public ICollection<FavoriteFolder> FavoriteFolders { get; set; } = new List<FavoriteFolder>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    [ForeignKey("LevelID")]
    public virtual UserLevel UserLevel { get; set; } = null!;
}
