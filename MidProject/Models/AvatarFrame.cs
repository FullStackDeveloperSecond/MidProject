using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class AvatarFrame
{
    public int FrameID { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, StringLength(10)]
    public string Rarity { get; set; } = "Common";

    public int PointsPrice { get; set; }
    public int? ImageID { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Image? Image { get; set; }
    public Member? DeletedByMember { get; set; }
    public ICollection<MemberAvatarFrame> MemberAvatarFrames { get; set; } = new List<MemberAvatarFrame>();
    public ICollection<Member> EquippedMembers { get; set; } = new List<Member>();
    public ICollection<PointsTransaction> PointsTransactions { get; set; } = new List<PointsTransaction>();
}
