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
        var today = _clock.GetNow().Date;
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
        var analytics = await TryLoadAnalyticsAsync(startOfMonth, cancellationToken);

        return new DashboardIndexViewModel
        {
            PendingReportCount = pendingReportCount.Count ?? 0,
            Analytics = analytics,
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

    private async Task<DashboardAnalyticsViewModel> TryLoadAnalyticsAsync(
        DateTime currentMonthStart,
        CancellationToken cancellationToken)
    {
        const string safeErrorCode = "DASHBOARD_ANALYTICS_QUERY_FAILED";
        try
        {
            var favoriteRows = await _dbContext.Restaurants
                .AsNoTracking()
                .Where(restaurant => !restaurant.IsDeleted)
                .Select(restaurant => new
                {
                    RestaurantID = restaurant.RestaurantID,
                    restaurant.Name,
                    FavoriteCount = restaurant.Favorites.Count(favorite => !favorite.IsDeleted)
                })
                .OrderByDescending(item => item.FavoriteCount)
                .ThenBy(item => item.Name)
                .Take(8)
                .ToListAsync(cancellationToken);

            var performanceRows = await _dbContext.Restaurants
                .AsNoTracking()
                .Where(restaurant => !restaurant.IsDeleted)
                .OrderByDescending(restaurant => restaurant.ReviewCount)
                .ThenByDescending(restaurant => restaurant.AverageRating)
                .ThenBy(restaurant => restaurant.Name)
                .Select(restaurant => new
                {
                    RestaurantID = restaurant.RestaurantID,
                    restaurant.Name,
                    restaurant.ReviewCount,
                    restaurant.AverageRating
                })
                .Take(8)
                .ToListAsync(cancellationToken);

            var earliestMonth = currentMonthStart.AddMonths(-5);
            var memberMonthlyRows = await _dbContext.Members
                .AsNoTracking()
                .Where(member => member.CreatedAt >= earliestMonth)
                .GroupBy(member => new { member.CreatedAt.Year, member.CreatedAt.Month })
                .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
                .ToListAsync(cancellationToken);
            var restaurantMonthlyRows = await _dbContext.Restaurants
                .AsNoTracking()
                .Where(restaurant => restaurant.CreatedAt >= earliestMonth)
                .GroupBy(restaurant => new { restaurant.CreatedAt.Year, restaurant.CreatedAt.Month })
                .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
                .ToListAsync(cancellationToken);
            var reviewMonthlyRows = await _dbContext.Reviews
                .AsNoTracking()
                .Where(review => review.CreatedAt >= earliestMonth)
                .GroupBy(review => new { review.CreatedAt.Year, review.CreatedAt.Month })
                .Select(group => new { group.Key.Year, group.Key.Month, Count = group.Count() })
                .ToListAsync(cancellationToken);

            var memberMonthly = memberMonthlyRows.ToDictionary(
                item => (item.Year, item.Month),
                item => item.Count);
            var restaurantMonthly = restaurantMonthlyRows.ToDictionary(
                item => (item.Year, item.Month),
                item => item.Count);
            var reviewMonthly = reviewMonthlyRows.ToDictionary(
                item => (item.Year, item.Month),
                item => item.Count);
            var monthlyActivity = Enumerable.Range(0, 6)
                .Select(offset =>
                {
                    var month = earliestMonth.AddMonths(offset);
                    var key = (month.Year, month.Month);
                    return new DashboardMonthlyActivityItem(
                        month.ToString("yyyy/MM"),
                        memberMonthly.GetValueOrDefault(key),
                        restaurantMonthly.GetValueOrDefault(key),
                        reviewMonthly.GetValueOrDefault(key));
                })
                .ToArray();

            var memberStatusRows = await _dbContext.Members
                .AsNoTracking()
                .GroupBy(member => member.IsDeleted ? "Deleted" : member.Status)
                .Select(group => new { Key = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
            var reportStatusRows = await _dbContext.Reports
                .AsNoTracking()
                .Where(report => !report.IsDeleted)
                .GroupBy(report => report.Status)
                .Select(group => new { Key = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            return new DashboardAnalyticsViewModel
            {
                FavoriteRestaurants = favoriteRows
                    .Select(item => new DashboardRestaurantFavoriteItem(
                        item.RestaurantID,
                        item.Name,
                        item.FavoriteCount))
                    .ToArray(),
                RestaurantPerformance = performanceRows
                    .Select(item => new DashboardRestaurantPerformanceItem(
                        item.RestaurantID,
                        item.Name,
                        item.ReviewCount,
                        item.AverageRating))
                    .ToArray(),
                MonthlyActivity = monthlyActivity,
                MemberStatuses = memberStatusRows
                    .OrderBy(item => MemberStatusOrder(item.Key))
                    .Select(item => new DashboardDistributionItem(
                        item.Key,
                        MemberStatusLabel(item.Key),
                        item.Count))
                    .ToArray(),
                ReportStatuses = reportStatusRows
                    .OrderBy(item => ReportStatusOrder(item.Key))
                    .Select(item => new DashboardDistributionItem(
                        item.Key,
                        ReportStatusLabel(item.Key),
                        item.Count))
                    .ToArray()
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Dashboard analytics query failed at {TaipeiTimestamp}; Operation=DashboardAnalytics; ResultClassification=Failed; SafeErrorCode={SafeErrorCode}; ExceptionType={ExceptionType}",
                _clock.GetNow(),
                safeErrorCode,
                exception.GetType().Name);
            return new DashboardAnalyticsViewModel
            {
                IsAvailable = false,
                UnavailableReason = $"統計圖表暫時無法取得，錯誤代碼：{safeErrorCode}"
            };
        }
    }

    private static int MemberStatusOrder(string status) => status switch
    {
        "Normal" => 0,
        "Warning" => 1,
        "Muted" => 2,
        "Suspended" => 3,
        "Deleted" => 4,
        _ => 5
    };

    private static string MemberStatusLabel(string status) => status switch
    {
        "Normal" => "正常",
        "Warning" => "警告",
        "Muted" => "禁言",
        "Suspended" => "停權",
        "Deleted" => "已刪除",
        _ => status
    };

    private static int ReportStatusOrder(string status) => status switch
    {
        "Pending" => 0,
        "Approved" => 1,
        "Rejected" => 2,
        _ => 3
    };

    private static string ReportStatusLabel(string status) => status switch
    {
        "Pending" => "待處理",
        "Approved" => "檢舉成立",
        "Rejected" => "駁回檢舉",
        _ => status
    };

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
