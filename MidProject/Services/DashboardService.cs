using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using MidProject.Data;
using MidProject.Models.ViewModels.Dashboard;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly AppDbContext _dbContext;
    private readonly IDashboardNotificationWindow _notificationWindow;
    private readonly ITaipeiClock _clock;
    private readonly ILogger<DashboardService> _logger;
    private readonly LinkGenerator _linkGenerator;

    public DashboardService(
        AppDbContext dbContext,
        IDashboardNotificationWindow notificationWindow,
        ITaipeiClock clock,
        ILogger<DashboardService> logger,
        LinkGenerator linkGenerator)
    {
        _dbContext = dbContext;
        _notificationWindow = notificationWindow;
        _clock = clock;
        _logger = logger;
        _linkGenerator = linkGenerator;
    }

    public async Task<DashboardIndexViewModel> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        var memberCount = await TryCountAsync("Members", () => _dbContext.Members
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted, cancellationToken), cancellationToken);
        var membersToday = await TryCountAsync("MembersToday", () => _dbContext.Members
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted && item.CreatedAt.Date == today, cancellationToken), cancellationToken);
        var restaurantCount = await TryCountAsync("Restaurants", () => _dbContext.Restaurants
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted, cancellationToken), cancellationToken);
        var restaurantsThisMonth = await TryCountAsync("RestaurantsThisMonth", () => _dbContext.Restaurants
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted && item.CreatedAt >= startOfMonth, cancellationToken), cancellationToken);
        var reviewCount = await TryCountAsync("Reviews", () => _dbContext.Reviews
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted, cancellationToken), cancellationToken);
        var reviewsToday = await TryCountAsync("ReviewsToday", () => _dbContext.Reviews
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted && item.CreatedAt.Date == today, cancellationToken), cancellationToken);
        var pendingReportCount = await TryCountAsync("Reports", () => _dbContext.Reports
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted && item.Status == "Pending", cancellationToken), cancellationToken);
        var notificationResult = await _notificationWindow.GetPendingSummaryAsync(cancellationToken);

        return new DashboardIndexViewModel
        {
            PendingReportCount = pendingReportCount.Count ?? 0,
            Cards =
            [
                BuildMetricCard(
                    "會員總數",
                    memberCount,
                    "目前未刪除的會員",
                    "fas fa-users",
                    "primary",
                    GetPath("Index", "AdminMembers"),
                    membersToday.IsSuccess ? $"↑ +{membersToday.Count} 今日" : "今日趨勢暫時無法取得"),
                BuildMetricCard(
                    "餐廳總數",
                    restaurantCount,
                    "目前啟用中的餐廳",
                    "fas fa-store",
                    "success",
                    GetPath("Index", "Restaurants"),
                    restaurantsThisMonth.IsSuccess ? $"本月新增 {restaurantsThisMonth.Count}" : "本月趨勢暫時無法取得"),
                BuildMetricCard(
                    "評論總數",
                    reviewCount,
                    "目前未刪除的評論",
                    "fas fa-comments",
                    "warning",
                    GetPath("Index", "Reviews"),
                    reviewsToday.IsSuccess ? $"↑ +{reviewsToday.Count} 今日" : "今日趨勢暫時無法取得"),
                BuildMetricCard(
                    "待處理檢舉數",
                    pendingReportCount,
                    "等待管理員審核",
                    "fas fa-flag",
                    "danger",
                    GetPath("Index", "Reports", new { Status = "Pending" })),
                notificationResult.Classification == DashboardNotificationClassification.Success
                    ? new(
                        "未發送通知數",
                        notificationResult.PendingCount,
                        "尚未發送且未刪除",
                        "fas fa-bell",
                        "purple",
                        true,
                        GetPath("Index", "Notifications", new { isSent = false }))
                    : new(
                        "未發送通知數",
                        null,
                        "通知統計查詢失敗",
                        "fas fa-bell",
                        "purple",
                        false,
                        null,
                        $"查詢失敗，錯誤代碼：{notificationResult.SafeErrorCode ?? "UNKNOWN"}")
            ]
        };
    }

    private string GetPath(string action, string controller, object? values = null)
    {
        return _linkGenerator.GetPathByAction(action, controller, values)
            ?? throw new InvalidOperationException(
                $"Could not generate the Dashboard route for {controller}/{action}.");
    }

    private async Task<DashboardMetricResult> TryCountAsync(
        string metricName,
        Func<Task<int>> query,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        try
        {
            return new(true, await query(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var safeErrorCode = $"{metricName.ToUpperInvariant()}_QUERY_FAILED";
            _logger.LogError(
                exception,
                "Dashboard metric query failed at {TaipeiTimestamp}; Operation=DashboardMetricCount; Metric={Metric}; ResultClassification=Failed; CorrelationID={CorrelationID}; ExceptionType={ExceptionType}",
                _clock.GetNow(),
                metricName,
                correlationId,
                exception.GetType().Name);
            return new(false, null, safeErrorCode);
        }
    }

    private static DashboardCardViewModel BuildMetricCard(
        string title,
        DashboardMetricResult result,
        string description,
        string iconClass,
        string accentClass,
        string targetUrl,
        string? trend = null) =>
        result.IsSuccess
            ? new(title, result.Count, description, iconClass, accentClass, true, targetUrl, Trend: trend)
            : new(
                title,
                null,
                $"{title}查詢失敗",
                iconClass,
                accentClass,
                false,
                null,
                $"查詢失敗，錯誤代碼：{result.SafeErrorCode ?? "UNKNOWN"}");

    private sealed record DashboardMetricResult(
        bool IsSuccess,
        int? Count,
        string? SafeErrorCode);
}
