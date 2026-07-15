using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class Image
{
    public int ImageID { get; set; }
    public int UploadedByMemberID { get; set; }

    [Required, StringLength(500)]
    public string ImageURL { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string ImageType { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Member? UploadedByMember { get; set; }
    public Member? DeletedByMember { get; set; }
    public ICollection<RestaurantImage> RestaurantImages { get; set; } = new List<RestaurantImage>();
    public ICollection<ReviewImage> ReviewImages { get; set; } = new List<ReviewImage>();
}
