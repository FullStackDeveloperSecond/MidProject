using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class Report
{
    public int ReportID { get; set; }
    public int ReporterMemberID { get; set; }
    public int? ReportedMemberID { get; set; }
    public int? RestaurantID { get; set; }
    public int? ReviewID { get; set; }
    public int? ImageID { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? HandledAt { get; set; }
    public int? HandledByMemberID { get; set; }
    public string? AdminNote { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Member? ReporterMember { get; set; }
    public Member? ReportedMember { get; set; }
    public Restaurant? Restaurant { get; set; }
    public Review? Review { get; set; }
    public Image? Image { get; set; }
    public Member? HandledByMember { get; set; }
    public Member? DeletedByMember { get; set; }
}
