using MidProject.Models.DTOs;
using MidProject.Models;

namespace MidProject.Repositories;

public interface IReportRepository
{
    Task<PagedResult<Report>> GetReportsAsync(ReportQueryParams query);
    Task<Report?> GetByIdAsync(int reportId);
    Task AddAsync(Report report);
    Task AddNotificationAsync(Notification notification);
    Task SaveChangesAsync();

    // 查詢某筆檢舉已送出的通知紀錄（依 Notifications.SourceReportID 關聯）
    Task<List<Notification>> GetNotificationsByReportAsync(int reportId);

    // Dashboard 用
    Task<(int pending, int approved, int rejected)> GetStatusCountsAsync();
    Task<int> GetPendingCountSinceAsync(DateTime since);
    Task<List<ReportStatusDatePoint>> GetStatusDatesSinceAsync(DateTime since);
}
