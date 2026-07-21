namespace MidProject.Models.ViewModels.Dashboard;

public sealed class DashboardIndexViewModel
{
    public IReadOnlyList<DashboardCardViewModel> Cards { get; init; } = [];
}

public sealed record DashboardCardViewModel(
    string Title,
    int? Count,
    string Description,
    string IconClass,
    string AccentClass,
    bool IsAvailable,
    string? TargetUrl,
    string? UnavailableReason = null);
