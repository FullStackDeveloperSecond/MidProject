namespace MidProject.Models.ViewModels.PointsStore;

public class AvatarFramesDeletedIndexViewModel
{
    public int TotalDeletedCount { get; set; }
    public DateTime? MostRecentDeletedAt { get; set; }
    public List<AvatarFrameDeletedRowViewModel> Frames { get; set; } = new();

    public string? Keyword { get; set; }
    public string? Rarity { get; set; }
    public string SortBy { get; set; } = "deletedAt";
    public int CurrentPage { get; set; } = 1;
    public int TotalItems { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}
