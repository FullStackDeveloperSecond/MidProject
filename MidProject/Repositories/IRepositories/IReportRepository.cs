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

    // 依檢舉目標（餐廳／評論／圖片）回推內容擁有者的 MemberID，供建立檢舉時填入被檢舉會員
    Task<int?> GetTargetOwnerMemberIdAsync(int? restaurantId, int? reviewId, int? imageId);

    // 查詢某筆檢舉的通知紀錄（依 Notifications.SourceReportID 關聯）
    Task<List<Notification>> GetNotificationsByReportAsync(int reportId);

    // Dashboard 用
    Task<(int pending, int approved, int rejected)> GetStatusCountsAsync();
    Task<int> GetPendingCountSinceAsync(DateTime since);
    Task<List<ReportStatusDatePoint>> GetStatusDatesSinceAsync(DateTime since);
}
