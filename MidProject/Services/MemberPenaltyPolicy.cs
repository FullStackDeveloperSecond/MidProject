using MidProject.Models;

namespace MidProject.Services;

// 集中管理「成立檢舉累積次數 → 會員處分」規則，讓即時檢舉處置與背景補償排程
// 使用完全相同的判定，避免兩條流程日後產生不同的懲處結果。
internal static class MemberPenaltyPolicy
{
    private static readonly Dictionary<string, int> StatusSeverity = new()
    {
        ["Normal"] = 0,
        ["Warning"] = 1,
        ["Muted"] = 2,
        ["Suspended"] = 3,
        ["Deleted"] = 4
    };

    public static bool Apply(
        Member member,
        int approvedCount,
        DateTime now,
        int? handledByMemberId)
    {
        if (member.Status == "Deleted" || approvedCount <= member.WarningCount)
        {
            return false;
        }

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
            return false;
        }

        // 管理員若已手動設定更嚴重的狀態，自動流程不得反向降級。
        if (StatusSeverity[targetStatus] < StatusSeverity.GetValueOrDefault(member.Status))
        {
            return false;
        }

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

        return true;
    }
}
