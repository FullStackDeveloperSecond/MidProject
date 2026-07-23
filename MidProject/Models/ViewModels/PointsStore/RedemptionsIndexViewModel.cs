namespace MidProject.Models.ViewModels.PointsStore;

public class RedemptionsIndexViewModel
{
    public int MonthlyRedemptionCount { get; set; }
    public int MonthlyPointsSpent { get; set; }
    public string? TopFrameName { get; set; }
    public int TopFrameCount { get; set; }
    public int DistinctMemberCount { get; set; }

    public List<RedemptionRowViewModel> Redemptions { get; set; } = new();
    public List<MidProject.Models.AvatarFrame> AvailableFrames { get; set; } = new();

    public string? Keyword { get; set; }
    public int? FrameFilter { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
}
