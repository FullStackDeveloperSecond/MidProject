using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class PointsTransaction
{
    public int TransactionID { get; set; }
    public int MemberID { get; set; }

    public int Amount { get; set; }
    public int BalanceAfter { get; set; }

    [Required, StringLength(20)]
    public string Type { get; set; } = string.Empty;

    public int? RelatedFrameID { get; set; }

    [StringLength(200)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? CreatedBy { get; set; }

    public Member? Member { get; set; }
    public AvatarFrame? RelatedFrame { get; set; }
    public Member? CreatedByMember { get; set; }
}
