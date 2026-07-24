namespace MidProject.Models.ViewModels.PointsStore;

public class RedemptionRowViewModel
{
    public DateTime RedeemedAt { get; set; }
    public int MemberID { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string? MemberAvatarUrl { get; set; }
    public int FrameID { get; set; }
    public string FrameName { get; set; } = string.Empty;
    public string? FrameImageUrl { get; set; }
    public int PointsSpent { get; set; }
    public int BalanceAfter { get; set; }
}
