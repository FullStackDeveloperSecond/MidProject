using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class Tag
{
    public int TagID { get; set; }

    [Required, StringLength(50)]
    public string TagName { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Member? DeletedByMember { get; set; }
    public ICollection<RestaurantTag> RestaurantTags { get; set; } = new List<RestaurantTag>();
}
