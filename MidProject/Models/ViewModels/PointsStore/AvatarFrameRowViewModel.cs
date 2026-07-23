namespace MidProject.Models.ViewModels.PointsStore;

public class AvatarFrameRowViewModel
{
    public int FrameID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public int PointsPrice { get; set; }
    public bool IsActive { get; set; }
    public string? ImageUrl { get; set; }
    public int RedeemedCount { get; set; }
}
