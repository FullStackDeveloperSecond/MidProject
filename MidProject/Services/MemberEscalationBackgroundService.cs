using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;

namespace MidProject.Services;

// 依累積受理（Approved）檢舉次數自動套用懲處等級：
// 1 次=警告、2 次=禁言 1 週、3 次=禁言 1 個月、4 次以上=停權並軟刪除。
// 只會升級，不會反向降級，也不會再變更已是 Deleted 的終止狀態。
// 原本這段邏輯放在 AdminMembersController 的 Index/Edit（GET）裡，等於瀏覽頁面就會寫資料庫，
// 且會員列表每一頁都對每個人各自查一次受理次數、各自呼叫一次 SaveChanges（N+1）。
// 改成排程背景服務：GET 恢復成單純讀取，懲處計算固定每 5 分鐘批次跑一次，
// 用兩個彙總查詢一次算完所有人的受理次數，最後統一呼叫一次 SaveChanges。
public sealed class MemberEscalationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<string, int> StatusSeverity = new()
    {
        ["Normal"] = 0,
        ["Warning"] = 1,
        ["Muted"] = 2,
        ["Suspended"] = 3,
        ["Deleted"] = 4
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberEscalationBackgroundService> _logger;

    public MemberEscalationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MemberEscalationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "自動懲處批次執行失敗");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 應用程式關閉，正常結束迴圈
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.Now;

        // 1. 處分期限已過的會員：批次恢復正常（單一 ExecuteUpdate，不逐筆查詢/儲存）
        var expiredCount = await context.Members
            .Where(m => m.Status != "Deleted" && m.Status != "Normal"
                     && m.PenaltyEndAt != null && m.PenaltyEndAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "Normal")
                .SetProperty(m => m.PenaltyEndAt, (DateTime?)null)
                .SetProperty(m => m.UpdatedAt, now), cancellationToken);

        // 2. 一次查出所有候選會員與所有人的受理檢舉次數（各一個彙總查詢，避免對每個會員各自查一次）
        var candidates = await context.Members
            .Where(m => m.Status != "Deleted")
            .Select(m => new { m.MemberID, m.Status, m.WarningCount })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            LogRun(expiredCount, 0);
            return;
        }

        var approvedCounts = await context.Reports
            .Where(r => r.Status == "Approved" && !r.IsDeleted && r.ReportedMemberID != null)
            .GroupBy(r => r.ReportedMemberID!.Value)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, cancellationToken);

        // 只有受理次數比上次套用時更高，才需要真的重新計算並追蹤該筆 Member 實體
        var idsNeedingEscalation = candidates
            .Where(m => approvedCounts.TryGetValue(m.MemberID, out var count) && count > m.WarningCount)
            .Select(m => m.MemberID)
            .ToList();

        if (idsNeedingEscalation.Count == 0)
        {
            LogRun(expiredCount, 0);
            return;
        }

        var membersToEscalate = await context.Members
            .Where(m => idsNeedingEscalation.Contains(m.MemberID))
            .ToListAsync(cancellationToken);

        foreach (var member in membersToEscalate)
        {
            ApplyEscalation(member, approvedCounts[member.MemberID], now);
        }

        // 3. 全部會員的變更一次性儲存，而不是每個人各自呼叫一次 SaveChanges
        await context.SaveChangesAsync(cancellationToken);

        LogRun(expiredCount, membersToEscalate.Count);
    }

    private static void ApplyEscalation(Member member, int approvedCount, DateTime now)
    {
        string targetStatus;
        DateTime? targetPenaltyEndAt;

        if (approvedCount >= 4)
        {
            targetStatus = "Suspended";
            targetPenaltyEndAt = null;
        }
        else if (approvedCount == 3)
        {
            targetStatus = "Muted";
            targetPenaltyEndAt = now.AddMonths(1);
        }
        else if (approvedCount == 2)
        {
            targetStatus = "Muted";
            targetPenaltyEndAt = now.AddDays(7);
        }
        else if (approvedCount == 1)
        {
            targetStatus = "Warning";
            targetPenaltyEndAt = null;
        }
        else
        {
            return;
        }

        // 不反向降級
        if (StatusSeverity[targetStatus] < StatusSeverity[member.Status]) return;

        member.Status = targetStatus;
        member.PenaltyEndAt = targetPenaltyEndAt;
        member.WarningCount = approvedCount;
        member.UpdatedAt = now;

        if (targetStatus == "Suspended")
        {
            member.IsDeleted = true;
            member.DeletedAt = now;
        }
    }

    private void LogRun(int expiredResetCount, int escalatedCount)
    {
        if (expiredResetCount == 0 && escalatedCount == 0) return;

        _logger.LogInformation(
            "自動懲處批次執行完成；處分期限到期恢復正常人數={ExpiredResetCount}；套用新懲處人數={EscalatedCount}",
            expiredResetCount, escalatedCount);
    }
}
