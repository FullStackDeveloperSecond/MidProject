namespace MidProject.Models;

public class Favorite
{
    public int FavoriteID { get; set; }
    public int MemberID { get; set; }
    public int RestaurantID { get; set; }
    public int FavoriteFolderID { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Member? Member { get; set; }
    public Restaurant? Restaurant { get; set; }
    public FavoriteFolder? FavoriteFolder { get; set; }
    public Member? DeletedByMember { get; set; }
}
