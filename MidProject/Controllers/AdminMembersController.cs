using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels;
using MidProject.Services;
using MidProject.Services.IServices;

[ServiceFilter(typeof(AdminAuthorizationFilter))]
public class AdminMembersController : Controller
{
    private readonly AppDbContext _context;
    private readonly ICurrentAdminAccessor _currentAdmin;

    public AdminMembersController(
        AppDbContext context,
        ICurrentAdminAccessor currentAdmin)
    {
        _context = context;
        _currentAdmin = currentAdmin;
    }

    
    // 4. 會員列表頁
    public async Task<IActionResult> Index(string keyword, string statusFilter, int? levelFilter, string sortBy, bool showAbnormal = false, bool todayOnly = false, int page = 1)
    {
        // 4.2 統計卡片資料
        ViewBag.TotalMembers = await _context.Members.CountAsync(m => !m.IsDeleted);
        ViewBag.TodayRegistered = await _context.Members.CountAsync(m => m.CreatedAt.Date == DateTime.Today && !m.IsDeleted);
        ViewBag.AbnormalCount = await _context.Members.CountAsync(m => !m.IsDeleted && m.Status != "Normal");

        // 基本查詢條件：軟刪除（含累積檢舉自動停權）的會員仍顯示在列表中（反灰、不可點擊），
        // 只是不計入「會員總數」等統計卡片
        var query = _context.Members.Include(m => m.UserLevel).Include(m => m.AvatarImage).AsQueryable();

        // 4.2 異常狀態會員按鈕觸發
        if (showAbnormal)
        {
            query = query.Where(m => m.Status != "Normal"); // 包含 Warning, Muted, Suspended
        }

        // 4.2 今日新註冊按鈕觸發
        if (todayOnly)
        {
            query = query.Where(m => m.CreatedAt.Date == DateTime.Today);
        }

        // 4.3 關鍵字搜尋 (Name/UserName, NickName, Email)
        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(m => m.UserName.Contains(keyword) || m.NickName.Contains(keyword) || m.Email.Contains(keyword));
        }

        // 4.3 篩選條件
        if (!string.IsNullOrEmpty(statusFilter)) query = query.Where(m => m.Status == statusFilter);
        if (levelFilter.HasValue) query = query.Where(m => m.LevelID == levelFilter.Value);

        // 4.5 排序 (支持 LV 與 Status 排序)
        query = sortBy switch
        {
            "lv_asc" => query.OrderBy(m => m.UserLevel.MinExp),
            "lv_desc" => query.OrderByDescending(m => m.UserLevel.MinExp),
            "status_asc" => query.OrderBy(m => m.Status),
            "status_desc" => query.OrderByDescending(m => m.Status),
            _ => query.OrderByDescending(m => m.CreatedAt) // 預設新到舊
        };

        // 4.7 分頁 (每頁 10 筆)
        int pageSize = 10;
        var totalItems = await query.CountAsync();
        var members = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // 依累積受理檢舉次數自動套用懲處（僅升級，不會反向降級）
        foreach (var m in members)
        {
            await ApplyAutoEscalationAsync(m);
        }

        // 狀態保留
        ViewBag.Keyword = keyword;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.LevelFilter = levelFilter;
        ViewBag.SortBy = sortBy;
        ViewBag.ShowAbnormal = showAbnormal;
        ViewBag.TodayOnly = todayOnly;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.Levels = await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync(); // 供篩選下拉選單與經驗值進度使用

        return View(members);
    }

    // 5. 會員詳細 / 編輯頁 (GET)
    public async Task<IActionResult> Edit(int id)
    {
        // 停權會員仍可開啟編輯頁（管理員可解除停權），故不排除 IsDeleted
        var member = await _context.Members
            .Include(m => m.UserLevel)
            .Include(m => m.AvatarImage)
            .FirstOrDefaultAsync(m => m.MemberID == id);
        if (member == null) return NotFound();

        // 依累積受理檢舉次數自動套用懲處（僅升級，不會反向降級）
        await ApplyAutoEscalationAsync(member);

        // 3.4 & 5.7 關聯檢舉紀錄：只顯示 Status = Approved (受理) 的紀錄
        ViewBag.ApprovedReports = await _context.Reports
            .Where(r => r.ReportedMemberID == id && r.Status == "Approved")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        ViewBag.Levels = await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync();

        return View(member);
    }

    // 5. 會員編輯儲存 (POST)
    // 改用專用的 MemberEditVM 繫結，避免 Member entity 上其他不相關欄位
    // （Email、PasswordHash、Role、UserLevel 等 non-nullable 導覽屬性）被 ASP.NET Core
    // 隱性視為必填，導致表單永遠驗證失敗、無法保存。
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MemberEditVM model)
    {
        if (id != model.MemberID) return NotFound();

        var memberInDb = await _context.Members.FirstOrDefaultAsync(m => m.MemberID == id);
        if (memberInDb == null) return NotFound();

        // 8. 驗證規則：狀態異動時 AdminNote 必填
        if (memberInDb.Status != model.Status && string.IsNullOrWhiteSpace(model.AdminNote))
        {
            ModelState.AddModelError(nameof(MemberEditVM.AdminNote), "變更會員狀態時，管理員備註（原因）為必填。");
        }

        if (ModelState.IsValid)
        {
            // 5.3 允許編輯欄位（帳號 UserName 不可變更；名稱改由專屬的 ChangeNickName 動作處理，故不在此表單接受變更）
            memberInDb.Status = model.Status;
            memberInDb.AdminNote = model.AdminNote;
            memberInDb.Points = model.Points;

            // 5.5 處分期限現在由自動懲處機制（ApplyAutoEscalationAsync）依受理檢舉次數計算，
            // 手動編輯僅在「正常／停權」時清空期限，其餘狀態維持既有的處分期限不變
            if (model.Status == "Normal" || model.Status == "Suspended")
            {
                memberInDb.PenaltyEndAt = null;
            }

            // 5.6 & 11. Status = Suspended 的連動規則（軟刪除：僅標記，不移除資料列；
            // 管理員仍可將狀態改回正常/警告以解除停權，此時清除軟刪除標記）
            if (model.Status == "Suspended")
            {
                memberInDb.IsDeleted = true;
                memberInDb.DeletedAt = DateTime.Now;
                memberInDb.DeletedBy = _currentAdmin.MemberID;
            }
            else
            {
                memberInDb.IsDeleted = false;
                memberInDb.DeletedAt = null;
                memberInDb.DeletedBy = null;
            }

            memberInDb.UpdatedAt = DateTime.Now;

            _context.Update(memberInDb);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // 驗證失敗時：重新查詢資料庫中完整且正確的會員資料（含等級、經驗值、點數、頭像等），
        // 只疊加使用者這次在表單中實際編輯的欄位，避免顯示未繫結欄位的預設值
        var displayMember = await _context.Members
            .Include(m => m.UserLevel)
            .Include(m => m.AvatarImage)
            .FirstOrDefaultAsync(m => m.MemberID == id);
        if (displayMember != null)
        {
            displayMember.Status = model.Status;
            displayMember.AdminNote = model.AdminNote;
            displayMember.Points = model.Points;
        }

        ViewBag.ApprovedReports = await _context.Reports
            .Where(r => r.ReportedMemberID == id && r.Status == "Approved")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        ViewBag.Levels = await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync();
        return View(displayMember);
    }

    // 5.3 會員名稱變更（獨立動作，需填寫原因）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeNickName(int id, string nickName, string reason)
    {
        var memberInDb = await _context.Members.FirstOrDefaultAsync(m => m.MemberID == id);
        if (memberInDb == null) return NotFound();

        if (string.IsNullOrWhiteSpace(nickName) || string.IsNullOrWhiteSpace(reason))
        {
            TempData["NicknameError"] = "請填寫新名稱與變更原因。";
            return RedirectToAction(nameof(Edit), new { id });
        }

        memberInDb.NickName = nickName;
        memberInDb.AdminNote = string.IsNullOrWhiteSpace(memberInDb.AdminNote)
            ? $"[名稱變更] {reason}"
            : $"[名稱變更] {reason}\n{memberInDb.AdminNote}";
        memberInDb.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    // 5.3 會員照片移除（僅能移除，改回預設照片；需填寫原因）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar(int id, string reason)
    {
        var memberInDb = await _context.Members.FirstOrDefaultAsync(m => m.MemberID == id);
        if (memberInDb == null) return NotFound();

        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["AvatarError"] = "請填寫移除照片的原因。";
            return RedirectToAction(nameof(Edit), new { id });
        }

        memberInDb.AvatarImageID = null;
        memberInDb.AdminNote = string.IsNullOrWhiteSpace(memberInDb.AdminNote)
            ? $"[照片移除] {reason}"
            : $"[照片移除] {reason}\n{memberInDb.AdminNote}";
        memberInDb.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Edit), new { id });
    }

    // 依累積受理（Approved）檢舉次數自動套用懲處等級：
    // 1 次=警告、2 次=禁言 1 週、3 次=禁言 1 個月、4 次以上=停權並軟刪除（會員列表仍反灰顯示，但不計入會員總數）。
    // 只會升級，不會反向降級，也不會再變更已是 Deleted 的終止狀態。
    private static readonly Dictionary<string, int> StatusSeverity = new()
    {
        ["Normal"] = 0,
        ["Warning"] = 1,
        ["Muted"] = 2,
        ["Suspended"] = 3,
        ["Deleted"] = 4
    };

    private async Task ApplyAutoEscalationAsync(Member member)
    {
        if (member.Status == "Deleted") return;

        // 1. 懲罰期限已過：狀態自動恢復為正常（僅限有期限的懲處，如禁言；停權為永久，需人工解除）
        if (member.PenaltyEndAt.HasValue && member.PenaltyEndAt.Value <= DateTime.Now)
        {
            member.Status = "Normal";
            member.PenaltyEndAt = null;
            member.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return;
        }

        var approvedCount = await _context.Reports
            .CountAsync(r => r.ReportedMemberID == member.MemberID && r.Status == "Approved" && !r.IsDeleted);

        string targetStatus;
        DateTime? targetPenaltyEndAt;
        string noteSuffix;

        if (approvedCount >= 4)
        {
            targetStatus = "Suspended";
            targetPenaltyEndAt = null;
            noteSuffix = $"累積 {approvedCount} 次受理檢舉，達第 4 次門檻，停權並移出會員總數";
        }
        else if (approvedCount == 3)
        {
            targetStatus = "Muted";
            targetPenaltyEndAt = DateTime.Now.AddMonths(1);
            noteSuffix = $"累積 {approvedCount} 次受理檢舉，禁言 1 個月";
        }
        else if (approvedCount == 2)
        {
            targetStatus = "Muted";
            targetPenaltyEndAt = DateTime.Now.AddDays(7);
            noteSuffix = $"累積 {approvedCount} 次受理檢舉，禁言 1 週";
        }
        else if (approvedCount == 1)
        {
            targetStatus = "Warning";
            targetPenaltyEndAt = null;
            noteSuffix = $"累積 {approvedCount} 次受理檢舉，警告";
        }
        else
        {
            return;
        }

        // 不反向降級；相同等級（例如 Muted -> Muted）仍會更新，以套用新的處分期限
        if (StatusSeverity[targetStatus] < StatusSeverity[member.Status]) return;
        if (targetStatus == member.Status && member.PenaltyEndAt == targetPenaltyEndAt) return;

        member.Status = targetStatus;
        member.PenaltyEndAt = targetPenaltyEndAt;
        member.AdminNote = string.IsNullOrWhiteSpace(member.AdminNote)
            ? $"[自動懲處] {noteSuffix}"
            : $"[自動懲處] {noteSuffix}\n{member.AdminNote}";
        member.UpdatedAt = DateTime.Now;

        if (targetStatus == "Suspended")
        {
            member.IsDeleted = true;
            member.DeletedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync();
    }
}
