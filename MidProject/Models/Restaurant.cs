using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MidProject.Models;

public class Restaurant
{
    public int RestaurantID { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string District { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string DetailedAddress { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal? Latitude { get; set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal? Longitude { get; set; }

    public int MemberID { get; set; }

    [Column(TypeName = "decimal(3,2)")]
    public decimal AverageRating { get; set; }

    public int ReviewCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    [StringLength(200)]
    public string? DeleteReason { get; set; }

    public Member? Member { get; set; }
    public Member? DeletedByMember { get; set; }
    public ICollection<BusinessHour> BusinessHours { get; set; } = new List<BusinessHour>();
    public ICollection<RestaurantTag> RestaurantTags { get; set; } = new List<RestaurantTag>();
    public ICollection<RestaurantImage> RestaurantImages { get; set; } = new List<RestaurantImage>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
