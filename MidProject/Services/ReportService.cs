using MidProject.Models.DTOs;
using MidProject.Models;
using MidProject.Repositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _repository;
    private readonly IReportNotificationWindow _notificationWindow;

    public ReportService(
        IReportRepository repository,
        IReportNotificationWindow notificationWindow)
    {
        _repository = repository;
        _notificationWindow = notificationWindow;
    }

    public async Task<PagedResult<ReportDto>> GetReportsAsync(ReportQueryParams query)
    {
        var paged = await _repository.GetReportsAsync(query);

        return new PagedResult<ReportDto>
        {
            Items = paged.Items.Select(ToDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<ReportDto?> GetByIdAsync(int reportId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        return report == null ? null : ToDto(report);
    }

    public async Task<Report> CreateReportAsync(ReportCreateDto dto, int reporterMemberId)
    {
        // 規則：RestaurantID / ReviewID / ImageID 只能有一個有值（使用者不能直接檢舉會員）
        var targetCount = new[] { dto.RestaurantID.HasValue, dto.ReviewID.HasValue, dto.ImageID.HasValue }
            .Count(x => x);

        if (targetCount != 1)
            throw new ArgumentException("檢舉目標必須恰好指定一個（餐廳、評論或圖片擇一）");

        var report = new Report
        {
            ReporterMemberID = reporterMemberId,
            RestaurantID = dto.RestaurantID,
            ReviewID = dto.ReviewID,
            ImageID = dto.ImageID,
            Reason = dto.Reason,
            Category = dto.Category,
            Status = "Pending",
            CreatedAt = DateTime.Now
        };

        await _repository.AddAsync(report);
        await _repository.SaveChangesAsync();

        return report;
    }

    public async Task<bool> HandleReportAsync(int reportId, ReportHandleDto dto, int adminMemberId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        if (report == null) return false;
        if (dto.Status is not ("Approved" or "Rejected")) return false;

        var reportedMemberId = report.Restaurant?.MemberID
            ?? report.Review?.MemberID
            ?? report.Image?.UploadedByMemberID;

        if (dto.Status == "Approved" &&
            (!reportedMemberId.HasValue ||
             string.IsNullOrWhiteSpace(dto.ReportedMemberNotificationTitle) ||
             string.IsNullOrWhiteSpace(dto.ReportedMemberNotificationContent)))
        {
            return false;
        }

        // 規則：核准/駁回檢舉只改 Report 本身的狀態，
        // 不會自動刪除或懲處被檢舉的目標（那是另外的手動動作）
        // 分類預設是檢舉人送出時選的，管理員審核時可以覆寫；沒選就維持原值
        report.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.Category))
            report.Category = dto.Category;
        report.AdminNote = dto.AdminNote;
        var handledAt = DateTime.Now;
        report.HandledAt = handledAt;
        report.HandledByMemberID = adminMemberId;

        var notificationResult = await _notificationWindow.CreateOutcomeNotificationsAsync(
            new ReportNotificationRequest(
                report.ReportID,
                report.ReporterMemberID,
                reportedMemberId,
                dto.Status,
                adminMemberId,
                handledAt,
                dto.ReporterNotificationTitle,
                dto.ReporterNotificationContent,
                dto.ReportedMemberNotificationTitle,
                dto.ReportedMemberNotificationContent));

        if (notificationResult.Classification == ReportNotificationClassification.AlreadyExists)
        {
            await _repository.SaveChangesAsync();
            return true;
        }

        return notificationResult.Classification == ReportNotificationClassification.Created;
    }

    public async Task<ReportDashboardDto> GetDashboardAsync()
    {
        var (pending, approved, rejected) = await _repository.GetStatusCountsAsync();

        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var pendingNewThisMonth = await _repository.GetPendingCountSinceAsync(monthStart);

        // 近 12 個月（含當月），先建立好每個月的桶子，確保沒有資料的月份也會顯示 0，而不是整根柱子消失
        var earliestMonth = monthStart.AddMonths(-11);
        var points = await _repository.GetStatusDatesSinceAsync(earliestMonth);

        var monthlyStats = new List<MonthlyReportStatDto>();
        for (var i = 0; i < 12; i++)
        {
            var bucketMonth = earliestMonth.AddMonths(i);
            var stat = new MonthlyReportStatDto
            {
                MonthLabel = bucketMonth.ToString("yyyy/MM"),
                Approved = points.Count(p => p.Status == "Approved" && p.CreatedAt.Year == bucketMonth.Year && p.CreatedAt.Month == bucketMonth.Month),
                Rejected = points.Count(p => p.Status == "Rejected" && p.CreatedAt.Year == bucketMonth.Year && p.CreatedAt.Month == bucketMonth.Month),
                Pending = points.Count(p => p.Status == "Pending" && p.CreatedAt.Year == bucketMonth.Year && p.CreatedAt.Month == bucketMonth.Month),
            };
            monthlyStats.Add(stat);
        }

        return new ReportDashboardDto
        {
            PendingCount = pending,
            ApprovedCount = approved,
            RejectedCount = rejected,
            PendingNewThisMonth = pendingNewThisMonth,
            MonthlyStats = monthlyStats
        };
    }

    private static ReportDto ToDto(Report r) => new()
    {
        ReportID = r.ReportID,
        ReporterMemberID = r.ReporterMemberID,
        ReporterUserName = r.ReporterMember?.UserName,
        RestaurantID = r.RestaurantID,
        // 不論目標類型是餐廳/評論/圖片，一律回推所屬餐廳名稱方便管理員辨識
        RestaurantName = r.Restaurant?.Name
            ?? r.Review?.Restaurant?.Name
            ?? r.Image?.RestaurantImages.Select(ri => ri.Restaurant?.Name).FirstOrDefault(n => n != null)
            ?? r.Image?.ReviewImages.Select(rvi => rvi.Review?.Restaurant?.Name).FirstOrDefault(n => n != null),
        ReviewID = r.ReviewID,
        ImageID = r.ImageID,
        // 被檢舉會員：該檢舉目標（餐廳/評論/圖片）背後的建立者/上傳者
        ReportedMemberUserName = r.Restaurant?.Member?.UserName
            ?? r.Review?.Member?.UserName
            ?? r.Image?.UploadedByMember?.UserName,
        Reason = r.Reason,
        Status = r.Status,
        Category = r.Category,
        CreatedAt = r.CreatedAt,
        HandledAt = r.HandledAt,
        HandledByUserName = r.HandledByMember?.UserName,
        AdminNote = r.AdminNote
    };
}
