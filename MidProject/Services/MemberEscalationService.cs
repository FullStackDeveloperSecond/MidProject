using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Services.IServices;

namespace MidProject.Services;

// 依累積受理（Approved）檢舉次數自動套用懲處等級：
// 1 次=警告、2 次=禁言 1 週、3 次=禁言 1 個月、4 次以上=停權並軟刪除。
// 只會升級，不會反向降級，也不會再變更已是 Deleted 的終止狀態。
// 這段邏輯獨立成 Scoped 服務（而不是寫死在 BackgroundService 裡），
// 讓 MemberEscalationBackgroundService 的排程跟 AdminMembersController 的手動「立即重新檢查」按鈕
// 可以共用同一份實作，兩邊算出來的結果保證一致。
public sealed class MemberEscalationService : IMemberEscalationService
{
    private static readonly Dictionary<string, int> StatusSeverity = new()
    {
        ["Normal"] = 0,
        ["Warning"] = 1,
        ["Muted"] = 2,
        ["Suspended"] = 3,
        ["Deleted"] = 4
    };

    private readonly AppDbContext _context;

    public MemberEscalationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MemberEscalationRunResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;

        // 1. 處分期限已過的會員：批次恢復正常（單一 ExecuteUpdate，不逐筆查詢/儲存）
        var expiredCount = await _context.Members
            .Where(m => m.Status != "Deleted" && m.Status != "Normal"
                     && m.PenaltyEndAt != null && m.PenaltyEndAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "Normal")
                .SetProperty(m => m.PenaltyEndAt, (DateTime?)null)
                .SetProperty(m => m.UpdatedAt, now), cancellationToken);

        // 2. 一次查出所有候選會員與所有人的受理檢舉次數（各一個彙總查詢，避免對每個會員各自查一次）
        var candidates = await _context.Members
            .Where(m => m.Status != "Deleted")
            .Select(m => new { m.MemberID, m.Status, m.WarningCount })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return new MemberEscalationRunResult(expiredCount, 0);
        }

        var approvedCounts = await _context.Reports
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
            return new MemberEscalationRunResult(expiredCount, 0);
        }

        var membersToEscalate = await _context.Members
            .Where(m => idsNeedingEscalation.Contains(m.MemberID))
            .ToListAsync(cancellationToken);

        // 這批人裡面這次會被判定「停權」（累積受理次數 >= 4）的，DeletedBy 要記錄「核准那筆檢舉的管理員」，
        // 不是系統自己。用該會員最近一筆已受理檢舉的 HandledByMemberID 當作停權操作者（一次查詢，非逐筆）。
        var suspendingIds = membersToEscalate
            .Where(m => approvedCounts[m.MemberID] >= 4)
            .Select(m => m.MemberID)
            .ToList();

        var handledByMemberLookup = new Dictionary<int, int?>();
        if (suspendingIds.Count > 0)
        {
            var suspendingReports = await _context.Reports
                .Where(r => r.ReportedMemberID != null && suspendingIds.Contains(r.ReportedMemberID.Value)
                         && r.Status == "Approved" && !r.IsDeleted)
                .Select(r => new { MemberId = r.ReportedMemberID!.Value, r.HandledByMemberID, r.HandledAt, r.CreatedAt })
                .ToListAsync(cancellationToken);

            handledByMemberLookup = suspendingReports
                .GroupBy(r => r.MemberId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(r => r.HandledAt ?? r.CreatedAt).First().HandledByMemberID);
        }

        foreach (var member in membersToEscalate)
        {
            handledByMemberLookup.TryGetValue(member.MemberID, out var handledByMemberId);
            ApplyEscalation(member, approvedCounts[member.MemberID], now, handledByMemberId);
        }

        // 3. 全部會員的變更一次性儲存，而不是每個人各自呼叫一次 SaveChanges
        await _context.SaveChangesAsync(cancellationToken);

        return new MemberEscalationRunResult(expiredCount, membersToEscalate.Count);
    }

    private static void ApplyEscalation(Member member, int approvedCount, DateTime now, int? handledByMemberId)
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
            member.DeletedBy = handledByMemberId;
        }
    }
}
