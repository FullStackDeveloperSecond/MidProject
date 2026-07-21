using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models.ViewModels.Dashboard;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class DashboardService : IDashboardService
{
    private const int DetailLimit = 10;

    private readonly AppDbContext _dbContext;
    private readonly IDashboardNotificationWindow _notificationWindow;

    public DashboardService(
        AppDbContext dbContext,
        IDashboardNotificationWindow notificationWindow)
    {
        _dbContext = dbContext;
        _notificationWindow = notificationWindow;
    }

    public async Task<DashboardIndexViewModel> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        var restaurantCount = await _dbContext.Restaurants
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted, cancellationToken);
        var reviewCount = await _dbContext.Reviews
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted, cancellationToken);
        var pendingReportCount = await _dbContext.Reports
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted && item.Status == "Pending", cancellationToken);
        var notificationResult = await _notificationWindow.GetPendingSummaryAsync(cancellationToken);

        return new DashboardIndexViewModel
        {
            Cards =
            [
                new(
                    "members",
                    "會員總數",
                    null,
                    "會員模組尚未整合",
                    "fas fa-users",
                    "primary",
                    false,
                    "保留位置，暫不連接會員資料或頁面。"),
                new(
                    "restaurants",
                    "餐廳總數",
                    restaurantCount,
                    "目前啟用中的餐廳",
                    "fas fa-store",
                    "success",
                    true),
                new(
                    "reviews",
                    "評論總數",
                    reviewCount,
                    "目前未刪除的評論",
                    "fas fa-comments",
                    "info",
                    true),
                new(
                    "pendingReports",
                    "待處理檢舉數",
                    pendingReportCount,
                    "等待管理員審核",
                    "fas fa-flag",
                    "warning",
                    true),
                notificationResult.Classification == DashboardNotificationClassification.Success
                    ? new(
                        "unsentNotifications",
                        "未發送通知數",
                        notificationResult.PendingCount,
                        "尚未發送且未刪除",
                        "fas fa-bell",
                        "danger",
                        true)
                    : new(
                        "unsentNotifications",
                        "未發送通知數",
                        null,
                        "通知統計查詢失敗",
                        "fas fa-bell",
                        "danger",
                        false,
                        $"查詢失敗，錯誤代碼：{notificationResult.SafeErrorCode ?? "UNKNOWN"}")
            ]
        };
    }

    public Task<DashboardDetailsViewModel?> GetDetailsAsync(
        string type,
        CancellationToken cancellationToken = default) => type switch
        {
            "members" => Task.FromResult<DashboardDetailsViewModel?>(MemberPlaceholder()),
            "restaurants" => GetRestaurantDetailsAsync(cancellationToken),
            "reviews" => GetReviewDetailsAsync(cancellationToken),
            "pendingReports" => GetPendingReportDetailsAsync(cancellationToken),
            "unsentNotifications" => GetUnsentNotificationDetailsAsync(cancellationToken),
            _ => Task.FromResult<DashboardDetailsViewModel?>(null)
        };

    private static DashboardDetailsViewModel MemberPlaceholder() => new()
    {
        Type = "members",
        Title = "會員總數",
        Description = "會員模組整合預留位置",
        QuerySummary = "本次不連接 Members 資料，也不建立會員管理路由。",
        IsAvailable = false,
        UnavailableReason = "會員模組尚未整合。"
    };

    private async Task<DashboardDetailsViewModel?> GetRestaurantDetailsAsync(CancellationToken cancellationToken)
    {
        var query = _dbContext.Restaurants.AsNoTracking().Where(item => !item.IsDeleted);
        var count = await query.CountAsync(cancellationToken);
        var data = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.RestaurantID)
            .Take(DetailLimit)
            .Select(item => new
            {
                item.RestaurantID,
                item.Name,
                Area = item.City + item.District,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var items = data
            .Select(item => new DashboardDetailRowViewModel([
                item.RestaurantID.ToString(),
                item.Name,
                item.Area,
                item.CreatedAt.ToString("yyyy/MM/dd HH:mm")
            ]))
            .ToList();

        return Detail(
            "restaurants",
            "餐廳總數",
            "目前啟用中的餐廳",
            count,
            "Restaurants where IsDeleted = false；顯示最新 10 筆。",
            ["ID", "餐廳名稱", "地區", "建立時間"],
            items,
            "/Restaurants",
            "前往餐廳管理");
    }

    private async Task<DashboardDetailsViewModel?> GetReviewDetailsAsync(CancellationToken cancellationToken)
    {
        var query = _dbContext.Reviews.AsNoTracking().Where(item => !item.IsDeleted);
        var count = await query.CountAsync(cancellationToken);
        var data = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.ReviewID)
            .Take(DetailLimit)
            .Select(item => new
            {
                item.ReviewID,
                item.RestaurantID,
                RestaurantName = item.Restaurant != null ? item.Restaurant.Name : null,
                item.MemberID,
                item.Rating,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var items = data
            .Select(item => new DashboardDetailRowViewModel([
                item.ReviewID.ToString(),
                item.RestaurantName ?? $"餐廳 #{item.RestaurantID}",
                item.MemberID.ToString(),
                item.Rating.ToString(),
                item.CreatedAt.ToString("yyyy/MM/dd HH:mm")
            ]))
            .ToList();

        return Detail(
            "reviews",
            "評論總數",
            "目前未刪除的評論",
            count,
            "Reviews where IsDeleted = false；顯示最新 10 筆。",
            ["ID", "餐廳", "評論者 ID", "評分", "建立時間"],
            items,
            "/Reviews",
            "前往評論管理");
    }

    private async Task<DashboardDetailsViewModel?> GetPendingReportDetailsAsync(CancellationToken cancellationToken)
    {
        var query = _dbContext.Reports
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.Status == "Pending");
        var count = await query.CountAsync(cancellationToken);
        var data = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.ReportID)
            .Take(DetailLimit)
            .Select(item => new
            {
                item.ReportID,
                item.Category,
                item.ReporterMemberID,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var items = data
            .Select(item => new DashboardDetailRowViewModel([
                item.ReportID.ToString(),
                item.Category,
                item.ReporterMemberID.ToString(),
                item.CreatedAt.ToString("yyyy/MM/dd HH:mm")
            ]))
            .ToList();

        return Detail(
            "pendingReports",
            "待處理檢舉數",
            "等待管理員審核的檢舉",
            count,
            "Reports where Status = Pending and IsDeleted = false；顯示最新 10 筆。",
            ["ID", "分類", "檢舉者 ID", "建立時間"],
            items,
            "/Reports?Status=Pending",
            "前往待處理檢舉");
    }

    private async Task<DashboardDetailsViewModel?> GetUnsentNotificationDetailsAsync(CancellationToken cancellationToken)
    {
        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(item => !item.IsDeleted && !item.IsSent);
        var count = await query.CountAsync(cancellationToken);
        var data = await query
            .OrderBy(item => item.ScheduledAt)
            .ThenBy(item => item.NotificationID)
            .Take(DetailLimit)
            .Select(item => new
            {
                item.NotificationID,
                item.NotificationType,
                item.Title,
                item.ScheduledAt
            })
            .ToListAsync(cancellationToken);
        var items = data
            .Select(item => new DashboardDetailRowViewModel([
                item.NotificationID.ToString(),
                item.NotificationType,
                item.Title,
                item.ScheduledAt.ToString("yyyy/MM/dd HH:mm")
            ]))
            .ToList();

        return Detail(
            "unsentNotifications",
            "未發送通知數",
            "尚未發送且未刪除的通知",
            count,
            "Notifications where IsSent = false and IsDeleted = false；顯示最接近排程時間的 10 筆。",
            ["ID", "類型", "標題", "排程時間"],
            items,
            "/Notifications?isSent=false",
            "前往未發送通知");
    }

    private static DashboardDetailsViewModel Detail(
        string type,
        string title,
        string description,
        int totalCount,
        string querySummary,
        IReadOnlyList<string> headers,
        IReadOnlyList<DashboardDetailRowViewModel> rows,
        string moduleUrl,
        string moduleLinkText) => new()
    {
        Type = type,
        Title = title,
        Description = description,
        TotalCount = totalCount,
        QuerySummary = querySummary,
        Headers = headers,
        Rows = rows,
        ModuleUrl = moduleUrl,
        ModuleLinkText = moduleLinkText
    };
}
