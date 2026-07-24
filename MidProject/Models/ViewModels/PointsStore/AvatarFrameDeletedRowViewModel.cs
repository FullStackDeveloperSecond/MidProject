namespace MidProject.Models.ViewModels.PointsStore;

public class AvatarFrameDeletedRowViewModel
{
    public int FrameID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public int PointsPrice { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByName { get; set; }
}
