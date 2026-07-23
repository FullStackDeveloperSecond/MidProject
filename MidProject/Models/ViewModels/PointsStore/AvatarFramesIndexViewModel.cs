namespace MidProject.Models.ViewModels.PointsStore;

public class AvatarFramesIndexViewModel
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public double AveragePointsPrice { get; set; }
    public int MonthlyRedemptionCount { get; set; }
    public int MonthlyPointsSpent { get; set; }
    public List<AvatarFrameRowViewModel> Frames { get; set; } = new();
}
