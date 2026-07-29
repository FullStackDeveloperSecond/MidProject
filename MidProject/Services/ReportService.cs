using MidProject.Models.DTOs;
using MidProject.Models;
using MidProject.Repositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class ReportService : IReportService
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.Ordinal)
    {
        "不實資訊", "廣告洗版", "人身攻擊", "仇恨言論", "色情內容", "垃圾訊息", "未分類"
    };

    private readonly IReportRepository _repository;
    private readonly IReportNotificationWindow _notificationWindow;
    private readonly ITaipeiClock _clock;

    public ReportService(IReportRepository repository, IReportNotificationWindow notificationWindow, ITaipeiClock clock)
    {
        _repository = repository;
        _notificationWindow = notificationWindow;
        _clock = clock;
    }

    public async Task<PagedResult<ReportDto>> GetReportsAsync(ReportQueryParams query)
    {
        var today = _clock.GetNow().Date;
        var paged = await _repository.GetReportsAsync(query, today);

        return new PagedResult<ReportDto>
        {
            Items = paged.Items.Select(report => ToDto(report, today)).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task<ReportDto?> GetByIdAsync(int reportId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        return report == null ? null : ToDto(report, _clock.GetNow().Date);
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
        if (reportedMemberId == reporterMemberId)
            throw new InvalidOperationException("不能檢舉自己的內容。");

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
            CreatedAt = _clock.GetNow()
        };

        await _repository.AddAsync(report);
        await _repository.SaveChangesAsync();

        return report;
    }

    public async Task<ReportHandleOutcome> HandleReportAsync(int reportId, ReportHandleDto dto, int adminMemberId)
    {
        // 只接受 Approved / Rejected（不接受 Pending）
        if (dto.Status != "Approved" && dto.Status != "Rejected")
            return ReportHandleOutcome.InvalidStatus;

        if (!string.IsNullOrWhiteSpace(dto.Category) && !AllowedCategories.Contains(dto.Category))
            return ReportHandleOutcome.InvalidCategory;

        // 管理員備註必填、空白視為未填、最多 30 字
        var note = dto.AdminNote?.Trim();
        if (string.IsNullOrEmpty(note)) return ReportHandleOutcome.AdminNoteRequired;
        if (note.Length > 30) return ReportHandleOutcome.AdminNoteTooLong;

        var report = await _repository.GetByIdAsync(reportId);
        if (report == null) return ReportHandleOutcome.NotFound;
        if (report.Status != "Pending") return ReportHandleOutcome.AlreadyHandled;

        // 回填被檢舉會員＝目標內容擁有者（排除 Admin，避免管理員被自動懲處停權）
        var reportedOwner = report.Restaurant?.Member
            ?? report.Review?.Member
            ?? report.Image?.UploadedByMember;
        if (reportedOwner?.MemberID == report.ReporterMemberID)
            return ReportHandleOutcome.SelfReportNotAllowed;

        var reportedMemberId = (reportedOwner != null && reportedOwner.Role != "Admin")
            ? reportedOwner.MemberID
            : (int?)null;

        // 分類：管理員可覆寫，沒選就維持原值
        var category = string.IsNullOrWhiteSpace(dto.Category) ? report.Category : dto.Category;

        // 並行控制：條件式原子更新（WHERE Status='Pending'），最多一人成功
        var affected = await _repository.TryHandleAsync(
            reportId, dto.Status, category, note, reportedMemberId, adminMemberId, _clock.GetNow());

        return affected > 0 ? ReportHandleOutcome.Handled : ReportHandleOutcome.AlreadyHandled;
    }

    // 通知檢舉者：管理員在通知視窗編輯內容後按「儲存」時呼叫。
    // 待處理案件在此時才定案（並行控制／備註驗證），再交由 Alex 的通知模組建立未發送通知。
    public async Task<ReportNotifyResult> NotifyReporterAsync(int reportId, NotifyReporterDto dto, int adminMemberId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        if (report == null) return ReportNotifyResult.Fail("找不到指定的檢舉。");

        var ensured = await EnsureHandledAsync(report, dto, adminMemberId);
        if (ensured.Error != null) return ensured.Error;
        report = ensured.Report!;

        if (report.Status == "Pending") return ReportNotifyResult.Fail("此檢舉尚未處理，無法通知。");

        var result = await _notificationWindow.CreateOutcomeNotificationAsync(
            new ReportNotificationRequest(reportId, report.ReporterMemberID, report.Status, adminMemberId,
                report.HandledAt ?? _clock.GetNow(), dto.Title, dto.Content));

        return ToNotifyResult(result);
    }

    // 通知被檢舉會員：僅檢舉成立時可用
    public async Task<ReportNotifyResult> NotifyReportedMemberAsync(int reportId, NotifyReporterDto dto, int adminMemberId)
    {
        var report = await _repository.GetByIdAsync(reportId);
        if (report == null) return ReportNotifyResult.Fail("找不到指定的檢舉。");

        var ensured = await EnsureHandledAsync(report, dto, adminMemberId);
        if (ensured.Error != null) return ensured.Error;
        report = ensured.Report!;

        if (report.Status != "Approved") return ReportNotifyResult.Fail("僅「檢舉成立」的案件才需通知被檢舉會員。");
        if (!report.ReportedMemberID.HasValue) return ReportNotifyResult.Fail("此檢舉沒有可通知的被檢舉會員。");

        var result = await _notificationWindow.CreateReportedMemberNotificationAsync(
            new ReportedMemberNotificationRequest(reportId, report.ReportedMemberID.Value, report.Status, adminMemberId,
                report.HandledAt ?? _clock.GetNow(), dto.Title, dto.Content));

        return ToNotifyResult(result);
    }

    // 待處理且帶有處理決定時，於此時才定案；成功則回傳最新的 Report，失敗則回傳明確的錯誤訊息。
    // 已處理者直接放行（不重新定案）。
    private async Task<(Report? Report, ReportNotifyResult? Error)> EnsureHandledAsync(Report report, NotifyReporterDto dto, int adminMemberId)
    {
        if (report.Status != "Pending") return (report, null);           // 已處理，直接通知
        if (string.IsNullOrWhiteSpace(dto.HandleStatus)) return (report, null); // 無處理決定，後續 pending 檢查會擋

        var outcome = await HandleReportAsync(report.ReportID, new ReportHandleDto
        {
            Status = dto.HandleStatus,
            Category = dto.HandleCategory,
            AdminNote = dto.HandleAdminNote
        }, adminMemberId);

        return outcome switch
        {
            ReportHandleOutcome.Handled => (await _repository.GetByIdAsync(report.ReportID), null),
            ReportHandleOutcome.AlreadyHandled => (null, ReportNotifyResult.Fail("此檢舉已由其他管理員處理，請重新整理後確認最新狀態。")),
            ReportHandleOutcome.InvalidStatus => (null, ReportNotifyResult.Fail("處理結果不正確（僅能為檢舉成立或駁回檢舉）。")),
            ReportHandleOutcome.InvalidCategory => (null, ReportNotifyResult.Fail("檢舉分類不正確。")),
            ReportHandleOutcome.SelfReportNotAllowed => (null, ReportNotifyResult.Fail("不允許處理會員檢舉自己內容的案件。")),
            ReportHandleOutcome.AdminNoteRequired => (null, ReportNotifyResult.Fail("處理檢舉時「管理員備註」為必填。")),
            ReportHandleOutcome.AdminNoteTooLong => (null, ReportNotifyResult.Fail("「管理員備註」最多 30 字。")),
            _ => (null, ReportNotifyResult.Fail("找不到指定的檢舉。"))
        };
    }

    // 通知結果對應明確訊息：會員不存在／已刪除、管理員無效、重複、保存失敗各自不同
    private static ReportNotifyResult ToNotifyResult(ReportNotificationResult result) => result.Classification switch
    {
        ReportNotificationClassification.Created => ReportNotifyResult.Ok(),
        ReportNotificationClassification.AlreadyExists => ReportNotifyResult.Already(),
        ReportNotificationClassification.MemberNotFound => ReportNotifyResult.Recipient("收件會員不存在，無法發送通知。"),
        ReportNotificationClassification.MemberDeleted => ReportNotifyResult.Recipient("收件會員已停權或刪除，無法發送通知；此檢舉已完成處理。"),
        ReportNotificationClassification.AdminInvalid => ReportNotifyResult.Fail("目前管理員帳號狀態異常（需為正常啟用的管理員），無法建立通知。"),
        ReportNotificationClassification.InvalidInput => ReportNotifyResult.Fail("通知資料不正確，無法建立。"),
        ReportNotificationClassification.InvalidOutcome => ReportNotifyResult.Fail("處理結果不正確，無法建立通知。"),
        ReportNotificationClassification.Failed => ReportNotifyResult.Fail("通知保存失敗，請稍後再試。"),
        _ => ReportNotifyResult.Fail("通知失敗，請稍後再試。")
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

        var now = _clock.GetNow();
        var monthStart = new DateTime(now.Year, now.Month, 1);
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

    private static ReportDto ToDto(Report r, DateTime today) => new()
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
        ReporterMemberDeleted = r.ReporterMember == null || r.ReporterMember.IsDeleted,
        ReportedMemberDeleted = IsReportedOwnerDeleted(r),
        Reason = r.Reason,
        Status = r.Status,
        Category = r.Category,
        CreatedAt = r.CreatedAt,
        HandledAt = r.HandledAt,
        HandledByUserName = r.HandledByMember?.UserName,
        AdminNote = r.AdminNote,
        ProcessingDays = r.Status == "Pending"
            ? Math.Max(0, (today - r.CreatedAt.Date).Days)
            : (r.HandledAt.HasValue ? (r.HandledAt.Value.Date - r.CreatedAt.Date).Days : 0)
    };

    // 被檢舉會員的有效 ID：目標內容擁有者，但擁有者為 Admin 時回傳 null（排除管理員）
    private static int? EffectiveReportedMemberId(Report r)
    {
        var owner = r.Restaurant?.Member ?? r.Review?.Member ?? r.Image?.UploadedByMember;
        return (owner != null && owner.Role != "Admin") ? owner.MemberID : null;
    }

    // 有效被檢舉會員（非 Admin）是否已停權/刪除——通知模組不會發給已刪除會員
    private static bool IsReportedOwnerDeleted(Report r)
    {
        var owner = r.Restaurant?.Member ?? r.Review?.Member ?? r.Image?.UploadedByMember;
        return owner != null && owner.Role != "Admin" && owner.IsDeleted;
    }
}
