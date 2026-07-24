using MidProject.Models.DTOs;
using MidProject.Models;
using MidProject.Repositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class ReportService : IReportService
{
    private readonly IReportRepository _repository;
    private readonly IReportNotificationWindow _notificationWindow;

    public ReportService(IReportRepository repository, IReportNotificationWindow notificationWindow)
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

        // 建立時即填入被檢舉會員＝檢舉目標（餐廳／評論／圖片）的擁有者
        var reportedMemberId = await _repository.GetTargetOwnerMemberIdAsync(dto.RestaurantID, dto.ReviewID, dto.ImageID);

        var report = new Report
        {
            ReporterMemberID = reporterMemberId,
            ReportedMemberID = reportedMemberId,
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

    // 通知檢舉者：管理員在通知視窗編輯內容後按「儲存」時呼叫，交由 Alex 的通知模組建立未發送通知。
    // 每個檢舉＋收件人只能通知一次（模組已存在該筆時回報 AlreadyNotified）。
    public async Task<ReportNotifyResult> NotifyReporterAsync(int reportId, NotifyReporterDto dto, int adminMemberId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        if (report == null) return ReportNotifyResult.Fail();

        // 待處理：按「儲存」的當下才把檢舉定案（用管理員的處理決定），再建立通知——
        // 未按儲存前檢舉維持待處理、不算處理完成
        report = await EnsureHandledAsync(report, dto, adminMemberId);
        if (report == null || report.Status == "Pending") return ReportNotifyResult.Fail();

        var handledAt = report.HandledAt ?? DateTime.Now;
        var result = await _notificationWindow.CreateOutcomeNotificationAsync(
            new ReportNotificationRequest(reportId, report.ReporterMemberID, report.Status, adminMemberId, handledAt, dto.Title, dto.Content));

        return ToNotifyResult(result);
    }

    // 通知被檢舉會員：僅檢舉成立時可用
    public async Task<ReportNotifyResult> NotifyReportedMemberAsync(int reportId, NotifyReporterDto dto, int adminMemberId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        if (report == null) return ReportNotifyResult.Fail();

        // 待處理：先定案再通知
        report = await EnsureHandledAsync(report, dto, adminMemberId);
        if (report == null || report.Status != "Approved") return ReportNotifyResult.Fail();
        if (!report.ReportedMemberID.HasValue) return ReportNotifyResult.Fail();

        var handledAt = report.HandledAt ?? DateTime.Now;
        var result = await _notificationWindow.CreateReportedMemberNotificationAsync(
            new ReportedMemberNotificationRequest(reportId, report.ReportedMemberID.Value, report.Status, adminMemberId, handledAt, dto.Title, dto.Content));

        return ToNotifyResult(result);
    }

    // 若檢舉仍為待處理且帶有處理決定，於此時才定案（設定狀態／被檢舉會員／處理人）；
    // 已處理者不動，重新讀取後回傳最新的 Report。
    private async Task<Report?> EnsureHandledAsync(Report report, NotifyReporterDto dto, int adminMemberId)
    {
        if (report.Status != "Pending") return report;
        if (string.IsNullOrWhiteSpace(dto.HandleStatus)) return report; // 沒有處理決定，維持待處理

        await HandleReportAsync(report.ReportID, new ReportHandleDto
        {
            Status = dto.HandleStatus,
            Category = dto.HandleCategory,
            AdminNote = dto.HandleAdminNote
        }, adminMemberId);

        return await _repository.GetByIdAsync(report.ReportID);
    }

    private static ReportNotifyResult ToNotifyResult(ReportNotificationResult result) => result.Classification switch
    {
        ReportNotificationClassification.Created => ReportNotifyResult.Ok(),
        ReportNotificationClassification.AlreadyExists => ReportNotifyResult.Already(),
        _ => ReportNotifyResult.Fail()
    };

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
            IsSent = n.IsSent,
            CreatedAt = n.CreatedAt,
            ScheduledAt = n.ScheduledAt,
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
        // 被檢舉會員：該檢舉目標（餐廳/評論/圖片）背後的建立者/上傳者。
        // 名稱維持顯示原擁有者；ID 則排除 Admin（管理員不列為被檢舉會員、不連結、不通知、不計懲處），
        // 與 HandleReportAsync／NotifyReportedMemberAsync 的規則一致
        ReportedMemberUserName = r.Restaurant?.Member?.UserName
            ?? r.Review?.Member?.UserName
            ?? r.Image?.UploadedByMember?.UserName,
        ReportedMemberID = EffectiveReportedMemberId(r),
        Reason = r.Reason,
        Status = r.Status,
        Category = r.Category,
        CreatedAt = r.CreatedAt,
        HandledAt = r.HandledAt,
        HandledByUserName = r.HandledByMember?.UserName,
        AdminNote = r.AdminNote
    };

    // 被檢舉會員的有效 ID：目標內容擁有者，但擁有者為 Admin 時回傳 null（排除管理員）
    private static int? EffectiveReportedMemberId(Report r)
    {
        var owner = r.Restaurant?.Member ?? r.Review?.Member ?? r.Image?.UploadedByMember;
        return (owner != null && owner.Role != "Admin") ? owner.MemberID : null;
    }
}
