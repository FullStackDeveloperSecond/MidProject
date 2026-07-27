using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels;
using MidProject.Services.IServices;
using System.Security.Claims;

[Authorize(Roles = "Admin")]
public class AdminMembersController : Controller
{
    private readonly AppDbContext _context;
    private readonly IMemberEscalationService _escalationService;

    public AdminMembersController(AppDbContext context, IMemberEscalationService escalationService)
    {
        _context = context;
        _escalationService = escalationService;
    }

    // 5.5 立即重新檢查懲處（POST，管理員手動觸發）：跟排程背景服務共用同一份 IMemberEscalationService，
    // 不是另一套邏輯。明確的使用者動作才會寫資料庫，跟 GET 頁面瀏覽脫鉤。
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalculateEscalation(int? returnToMemberId)
    {
        var result = await _escalationService.RunOnceAsync();
        TempData["EscalationResult"] =
            $"已重新整理。";

        return returnToMemberId.HasValue
            ? RedirectToAction(nameof(Edit), new { id = returnToMemberId.Value })
            : RedirectToAction(nameof(Index));
    }

    // 4. 會員列表頁
    // 純讀取，不寫入資料庫：自動懲處計算已改由 MemberEscalationBackgroundService 排程批次處理，
    // 這裡看到的 Status / PenaltyEndAt 是該排程上次執行後的結果，不會因為開這個頁面而被修改。
    public async Task<IActionResult> Index(string keyword, string statusFilter, int? levelFilter, string sortBy, bool showAbnormal = false, bool todayOnly = false, int page = 1)
    {
        // 統計卡片、列表都是各自獨立的查詢；這個頁面本身雖然不再寫資料庫，
        // 但自動懲處排程（MemberEscalationBackgroundService）仍會每 5 分鐘在背景寫入 Members 表。
        // 用一個唯讀交易（RepeatableRead）把本次請求的所有查詢包在同一個快照裡，
        // 避免萬一排程剛好在這幾個查詢中間執行，導致統計數字跟下面的列表內容對不上。
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

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

        await transaction.CommitAsync();

        return View(members);
    }

    // 5. 會員詳細 / 編輯頁 (GET)
    // 純讀取，不寫入資料庫：自動懲處計算已改由 MemberEscalationBackgroundService 排程批次處理（見該檔案）。
    public async Task<IActionResult> Edit(int id)
    {
        // 停權會員仍可開啟編輯頁（管理員可解除停權），故不排除 IsDeleted
        var member = await _context.Members
            .Include(m => m.UserLevel)
            .Include(m => m.AvatarImage)
            .FirstOrDefaultAsync(m => m.MemberID == id);
        if (member == null) return NotFound();

        // 3.4 & 5.7 關聯檢舉紀錄：只顯示 Status = Approved (受理) 的紀錄
        ViewBag.ApprovedReports = await _context.Reports
            .Where(r => r.ReportedMemberID == id && r.Status == "Approved")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        ViewBag.Levels = await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync();
        ViewBag.OriginalStatus = member.Status;
        ViewBag.OriginalNickName = member.NickName;
        ViewBag.OriginalPoints = member.Points;

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

        // 每個「手動調整」子動作（改名稱／移除照片／調整點數／解除鎖定／變更狀態）都先在前端彈窗確認、
        // 即時把變更疊加預覽進 AdminNote 文字框並帶入對應的隱藏欄位，尚未真的送出到資料庫；
        // 要按下這個表單的「保存變更」才會一次性套用所有已預覽的變更並真正保存（跟「解除停權」原本的流程一致）
        var statusChanged = memberInDb.Status != model.Status;
        var nicknameChanged = model.NickName != null && memberInDb.NickName != model.NickName;
        var avatarRemoval = model.RemoveAvatarRequested && memberInDb.AvatarImageID != null;
        var pointsChanged = memberInDb.Points != model.Points;
        var unlockRequested = model.UnlockAccountRequested && memberInDb.IsLocked;

        if (statusChanged && string.IsNullOrWhiteSpace(model.StatusChangeReason))
        {
            ModelState.AddModelError(nameof(MemberEditVM.StatusChangeReason), "變更會員狀態時，請填寫變更原因。");
        }
        if (nicknameChanged && string.IsNullOrWhiteSpace(model.NicknameChangeReason))
        {
            ModelState.AddModelError(nameof(MemberEditVM.NicknameChangeReason), "變更名稱時，請填寫變更原因。");
        }
        if (avatarRemoval && string.IsNullOrWhiteSpace(model.AvatarRemovalReason))
        {
            ModelState.AddModelError(nameof(MemberEditVM.AvatarRemovalReason), "移除照片時，請填寫原因。");
        }
        if (pointsChanged && string.IsNullOrWhiteSpace(model.PointsChangeReason))
        {
            ModelState.AddModelError(nameof(MemberEditVM.PointsChangeReason), "調整點數時，請填寫原因。");
        }
        // 解除鎖定原因為選填，不需驗證

        if (ModelState.IsValid)
        {
            // 5.3 允許編輯欄位（帳號 UserName 不可變更）
            // 各子動作變更時，前端已經把「時間 已將X從A變更為B，原因：xxx」疊加寫進 AdminNote 文字框中預覽；
            // 這裡直接保存文字框當下的內容即可，送出前使用者仍可自由編輯這段預覽文字
            memberInDb.AdminNote = model.AdminNote;
            memberInDb.Status = model.Status;

            if (nicknameChanged)
            {
                memberInDb.NickName = model.NickName;
            }
            if (avatarRemoval)
            {
                memberInDb.AvatarImageID = null;
            }
            if (pointsChanged)
            {
                memberInDb.Points = model.Points;
            }
            if (unlockRequested)
            {
                memberInDb.IsLocked = false;
                memberInDb.FailedLoginCount = 0;
            }

            // 5.5 處分期限現在由自動懲處排程（MemberEscalationBackgroundService）依受理檢舉次數計算，
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
                var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(adminIdClaim, out var adminId))
                {
                    memberInDb.DeletedBy = adminId;
                }
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
            if (model.NickName != null)
            {
                displayMember.NickName = model.NickName;
            }
            if (model.RemoveAvatarRequested)
            {
                displayMember.AvatarImageID = null;
                displayMember.AvatarImage = null;
            }
            displayMember.Points = model.Points;
        }

        ViewBag.ApprovedReports = await _context.Reports
            .Where(r => r.ReportedMemberID == id && r.Status == "Approved")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        ViewBag.Levels = await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync();
        ViewBag.StatusChangeReason = model.StatusChangeReason;
        ViewBag.OriginalStatus = memberInDb.Status;
        ViewBag.OriginalNickName = memberInDb.NickName;
        ViewBag.OriginalPoints = memberInDb.Points;
        ViewBag.NicknameChangeReason = model.NicknameChangeReason;
        ViewBag.RemoveAvatarRequested = model.RemoveAvatarRequested;
        ViewBag.AvatarRemovalReason = model.AvatarRemovalReason;
        ViewBag.PointsChangeReason = model.PointsChangeReason;
        ViewBag.UnlockAccountRequested = model.UnlockAccountRequested;
        ViewBag.UnlockReason = model.UnlockReason;
        return View(displayMember);
    }

    // 自動懲處計算（依累積受理檢舉次數升級 Status / PenaltyEndAt）已搬到 MemberEscalationBackgroundService
    // 排程背景服務，每 5 分鐘批次執行一次；這個 Controller 不再自己算、也不再在 GET 裡寫資料庫。
}