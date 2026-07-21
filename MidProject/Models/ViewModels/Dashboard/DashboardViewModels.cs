namespace MidProject.Models.ViewModels.Dashboard;

public sealed class DashboardIndexViewModel
{
    public IReadOnlyList<DashboardCardViewModel> Cards { get; init; } = [];
}

public sealed record DashboardCardViewModel(
    string Type,
    string Title,
    int? Count,
    string Description,
    string IconClass,
    string AccentClass,
    bool IsAvailable,
    string? UnavailableReason = null);

public sealed class DashboardDetailsViewModel
{
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int? TotalCount { get; init; }
    public string QuerySummary { get; init; } = string.Empty;
    public bool IsAvailable { get; init; } = true;
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyList<DashboardDetailRowViewModel> Rows { get; init; } = [];
    public string? ModuleUrl { get; init; }
    public string? ModuleLinkText { get; init; }
}

public sealed record DashboardDetailRowViewModel(IReadOnlyList<string> Values);
