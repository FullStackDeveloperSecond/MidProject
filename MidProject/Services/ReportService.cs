using MidProject.Models.DTOs;
using MidProject.Models;
using MidProject.Repositories;

namespace MidProject.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _repository;

    public ReportService(IReportRepository repository)
    {
        _repository = repository;
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

        // 規則：核准/駁回檢舉只改 Report 本身的狀態，
        // 不會自動刪除或懲處被檢舉的目標（那是另外的手動動作）
        // 分類預設是檢舉人送出時選的，管理員審核時可以覆寫；沒選就維持原值
        report.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.Category))
            report.Category = dto.Category;
        report.AdminNote = dto.AdminNote;
        report.HandledAt = DateTime.Now;
        report.HandledByMemberID = adminMemberId;

        // 回填「被檢舉會員」＝該檢舉目標（餐廳/評論/圖片）背後的建立者/上傳者。
        // AdminMembers/Edit 的「檢舉累積次數」與自動懲處是以 Reports.ReportedMemberID + Status=Approved 計數，
        // 若這裡不回填，被檢舉會員的累積次數永遠是 0，故在管理員處理檢舉時一併寫入。
        // 但排除 Admin：管理員被自動懲處停權會讓通知模組的固定管理員失格、啟動驗證失敗。
        var reportedOwner = report.Restaurant?.Member
            ?? report.Review?.Member
            ?? report.Image?.UploadedByMember;
        report.ReportedMemberID = (reportedOwner != null && reportedOwner.Role != "Admin")
            ? reportedOwner.MemberID
            : null;

        await _repository.SaveChangesAsync();
        return true;
    }

    public async Task<bool> NotifyReporterAsync(int reportId, NotifyReporterDto dto, int adminMemberId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        if (report == null) return false;

        // 規則：只有已經審核完畢（核准或駁回）的檢舉才能通知結果，Pending 的還沒有結果可以通知
        if (report.Status == "Pending") return false;

        var notification = new Notification
        {
            MemberID = report.ReporterMemberID,
            NotificationType = "Personal",
            Title = dto.Title,
            Content = dto.Content,
            ScheduledAt = DateTime.Now,
            SentAt = DateTime.Now,
            IsSent = true,
            CreatedAt = DateTime.Now,
            CreatedBy = adminMemberId,
            // 寫入來源檢舉標記，讓「通知紀錄」查得到已送出內容
            //（索引已改為非唯一，同一檢舉可掛多筆通知）
            SourceReportID = reportId,
            SourceReportOutcome = report.Status
        };

        await _repository.AddNotificationAsync(notification);
        await _repository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> NotifyReportedMemberAsync(int reportId, NotifyReporterDto dto, int adminMemberId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        if (report == null) return false;

        // 規則：只有「檢舉成立」才需要通知內容擁有者，駁回代表內容沒有違規，不需要通知
        if (report.Status != "Approved") return false;

        var reportedMemberId = report.Restaurant?.MemberID
            ?? report.Review?.MemberID
            ?? report.Image?.UploadedByMemberID;
        if (reportedMemberId == null) return false;

        var notification = new Notification
        {
            MemberID = reportedMemberId,
            NotificationType = "Personal",
            Title = dto.Title,
            Content = dto.Content,
            ScheduledAt = DateTime.Now,
            SentAt = DateTime.Now,
            IsSent = true,
            CreatedAt = DateTime.Now,
            CreatedBy = adminMemberId,
            // 通知被檢舉會員也掛來源檢舉標記（索引已非唯一），通知紀錄才能一併列出
            SourceReportID = reportId,
            SourceReportOutcome = report.Status
        };

        await _repository.AddNotificationAsync(notification);
        await _repository.SaveChangesAsync();

        return true;
    }

    public async Task<List<ReportNotificationRecordDto>> GetSentNotificationsAsync(int reportId)
    {
        var notifications = await _repository.GetNotificationsByReportAsync(reportId);
        return notifications.Select(n => new ReportNotificationRecordDto
        {
            NotificationID = n.NotificationID,
            MemberID = n.MemberID,
            Title = n.Title,
            Content = n.Content,
            Outcome = n.SourceReportOutcome,
            SentAt = n.SentAt
        }).ToList();
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
        ReportedMemberID = r.Restaurant?.MemberID
            ?? r.Review?.MemberID
            ?? r.Image?.UploadedByMemberID,
        Reason = r.Reason,
        Status = r.Status,
        Category = r.Category,
        CreatedAt = r.CreatedAt,
        HandledAt = r.HandledAt,
        HandledByUserName = r.HandledByMember?.UserName,
        AdminNote = r.AdminNote
    };
}
