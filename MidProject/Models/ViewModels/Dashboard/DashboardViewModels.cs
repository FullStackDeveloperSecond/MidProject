namespace MidProject.Models.ViewModels.Dashboard;

public sealed class DashboardIndexViewModel
{
    public IReadOnlyList<DashboardCardViewModel> Cards { get; init; } = [];
    public int PendingReportCount { get; init; }
    public DashboardAnalyticsViewModel Analytics { get; init; } = new();
}

public sealed record DashboardCardViewModel(
    string Title,
    int? Count,
    string Description,
    string IconClass,
    string AccentClass,
    bool IsAvailable,
    string? TargetUrl,
    string? UnavailableReason = null,
    string? Trend = null);

public sealed class DashboardAnalyticsViewModel
{
    public bool IsAvailable { get; init; } = true;
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<DashboardRestaurantFavoriteItem> FavoriteRestaurants { get; init; } = [];
    public IReadOnlyList<DashboardRestaurantPerformanceItem> RestaurantPerformance { get; init; } = [];
    public IReadOnlyList<DashboardMonthlyActivityItem> MonthlyActivity { get; init; } = [];
    public IReadOnlyList<DashboardDistributionItem> MemberStatuses { get; init; } = [];
    public IReadOnlyList<DashboardDistributionItem> ReportStatuses { get; init; } = [];
}

public sealed record DashboardRestaurantFavoriteItem(
    int RestaurantID,
    string Name,
    int FavoriteCount);

public sealed record DashboardRestaurantPerformanceItem(
    int RestaurantID,
    string Name,
    int ReviewCount,
    decimal AverageRating);

public sealed record DashboardMonthlyActivityItem(
    string MonthLabel,
    int MemberCount,
    int RestaurantCount,
    int ReviewCount);

public sealed record DashboardDistributionItem(
    string Key,
    string Label,
    int Count);
