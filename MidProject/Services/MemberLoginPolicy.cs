using MidProject.Models;

namespace MidProject.Services;

// 登入資格判斷 + 失敗鎖定規則抽成純函式（沒有任何 DbContext／HttpContext 依賴），
// 方便直接寫單元測試涵蓋「登入資格」與「連續密碼錯誤鎖定」這兩條規則，
// 不用為了測試而模擬整個 ASP.NET Core 驗證管線。
public static class MemberLoginPolicy
{
    public const int MaxFailedAttempts = 3;

    // 帳號本身是否允許嘗試登入（跟密碼是否正確無關）。回傳 null 代表合格；
    // 有值代表應該顯示的拒絕訊息。
    public static string? CheckEligibility(Member member)
    {
        // 這個網站是後台管理系統，只給 Admin 用；一般 User 角色的會員帳號（例如公開註冊產生的）
        // 一律擋在登入這關，不像過去那樣還放行、事後才在每個功能各自被 [Authorize(Roles="Admin")] 擋下。
        if (member.Role != "Admin")
        {
            return "此系統僅供管理員登入使用。";
        }
        if (member.Status == "Suspended")
        {
            return "帳號目前為停權狀態，請聯繫管理員。";
        }
        if (member.IsDeleted || member.Status == "Deleted")
        {
            return "帳號已刪除，請聯繫管理員。";
        }
        if (member.IsLocked)
        {
            return "帳號已因密碼輸入錯誤過多次被鎖定，請聯繫管理員解除鎖定。";
        }
        if (!member.IsActive)
        {
            return "帳號已停用，請聯繫管理員。";
        }
        return null;
    }

    // 密碼驗證失敗時，算出新的失敗次數，以及這次是否要順便鎖定帳號。
    public static (int NewFailedCount, bool ShouldLock) RecordFailedAttempt(int currentFailedCount)
    {
        var newFailedCount = currentFailedCount + 1;
        return (newFailedCount, newFailedCount >= MaxFailedAttempts);
    }
}
