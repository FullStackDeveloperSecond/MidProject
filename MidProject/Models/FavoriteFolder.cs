using System.ComponentModel.DataAnnotations;

namespace MidProject.Models;

public class FavoriteFolder
{
    public int FavoriteFolderID { get; set; }
    public int MemberID { get; set; }

    [Required, StringLength(100)]
    public string FolderName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedBy { get; set; }

    public Member? Member { get; set; }
    public Member? DeletedByMember { get; set; }
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
