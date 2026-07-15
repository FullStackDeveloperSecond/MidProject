using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class Notification
{
    public int NotificationID { get; set; }
    public int? MemberID { get; set; }

    [Required, StringLength(20)]
    public string NotificationType { get; set; } = "Personal";

    [StringLength(10)]
    public string? TargetRole { get; set; }

    [StringLength(20)]
    public string? TargetStatus { get; set; }

    public int? TargetLevelID { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public bool IsSent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Member? Member { get; set; }
    public UserLevel? TargetLevel { get; set; }
    public Member? CreatedByMember { get; set; }
    public Member? DeletedByMember { get; set; }
}
