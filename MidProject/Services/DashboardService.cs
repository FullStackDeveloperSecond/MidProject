using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models.ViewModels.Dashboard;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class DashboardService : IDashboardService
{
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
        var memberCount = await _dbContext.Members
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted, cancellationToken);
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
                    "會員總數",
                    memberCount,
                    "目前未刪除的會員",
                    "fas fa-users",
                    "primary",
                    true,
                    "/AdminMembers"),
                new(
                    "餐廳總數",
                    restaurantCount,
                    "目前啟用中的餐廳",
                    "fas fa-store",
                    "success",
                    true,
                    "/Restaurants"),
                new(
                    "評論總數",
                    reviewCount,
                    "目前未刪除的評論",
                    "fas fa-comments",
                    "info",
                    true,
                    "/Reviews"),
                new(
                    "待處理檢舉數",
                    pendingReportCount,
                    "等待管理員審核",
                    "fas fa-flag",
                    "warning",
                    true,
                    "/Reports?Status=Pending"),
                notificationResult.Classification == DashboardNotificationClassification.Success
                    ? new(
                        "未發送通知數",
                        notificationResult.PendingCount,
                        "尚未發送且未刪除",
                        "fas fa-bell",
                        "danger",
                        true,
                        "/Notifications?isSent=false")
                    : new(
                        "未發送通知數",
                        null,
                        "通知統計查詢失敗",
                        "fas fa-bell",
                        "danger",
                        false,
                        null,
                        $"查詢失敗，錯誤代碼：{notificationResult.SafeErrorCode ?? "UNKNOWN"}")
            ]
        };
    }
}
