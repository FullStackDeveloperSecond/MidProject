namespace MidProject.Services.IServices;

public interface IDashboardNotificationWindow
{
    Task<DashboardNotificationSummaryResult> GetPendingSummaryAsync(CancellationToken cancellationToken = default);
}

public enum DashboardNotificationClassification
{
    Success,
    Failed
}

public sealed record DashboardNotificationSummaryResult(
    DashboardNotificationClassification Classification,
    int? PendingCount,
    string IndexController,
    string IndexAction,
    IReadOnlyDictionary<string, object> IndexQuery,
    string? SafeErrorCode,
    string CorrelationID);
