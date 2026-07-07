using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class Review
{
    public int ReviewID { get; set; }
    public int MemberID { get; set; }
    public int RestaurantID { get; set; }
    public int Rating { get; set; }
    public string? Content { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Active";

    public int ReportCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public Member? Member { get; set; }
    public Restaurant? Restaurant { get; set; }
    public Member? DeletedByMember { get; set; }
    public ICollection<ReviewImage> ReviewImages { get; set; } = new List<ReviewImage>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
