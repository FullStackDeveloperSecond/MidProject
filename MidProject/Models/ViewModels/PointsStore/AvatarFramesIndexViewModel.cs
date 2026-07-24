namespace MidProject.Models.ViewModels.PointsStore;

public class AvatarFramesIndexViewModel
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public double AveragePointsPrice { get; set; }
    public int MonthlyRedemptionCount { get; set; }
    public int MonthlyPointsSpent { get; set; }
    public List<AvatarFrameRowViewModel> Frames { get; set; } = new();

    public string? Keyword { get; set; }
    public string? Rarity { get; set; }
    public bool? IsActive { get; set; }
    public string SortBy { get; set; } = "newest";
    public int CurrentPage { get; set; } = 1;
    public int TotalItems { get; set; }
    public int PageSize { get; set; } = 10;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}
