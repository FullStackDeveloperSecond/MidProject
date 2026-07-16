using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels;
using System.Security.Claims;

[Authorize(Roles = "Admin")]
public class AdminMembersController : Controller
{
    private readonly AppDbContext _context;

    public AdminMembersController(AppDbContext context)
    {
        _context = context;
    }

    
    // 4. 會員列表頁
    public async Task<IActionResult> Index(string keyword, string statusFilter, int? levelFilter, string sortBy, bool showAbnormal = false, bool todayOnly = false, int page = 1)
    {
        // 4.2 統計卡片資料
        ViewBag.TotalMembers = await _context.Members.CountAsync(m => !m.IsDeleted);
        ViewBag.TodayRegistered = await _context.Members.CountAsync(m => m.CreatedAt.Date == DateTime.Today && !m.IsDeleted);

        // 基本查詢條件：Deleted 會員不顯示在一般與異常列表
        var query = _context.Members.Include(m => m.UserLevel).Include(m => m.AvatarImage).Where(m => !m.IsDeleted);

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

        return View(members);
    }

    // 5. 會員詳細 / 編輯頁 (GET)
    public async Task<IActionResult> Edit(int id)
    {
        var member = await _context.Members
            .Include(m => m.UserLevel)
            .Include(m => m.AvatarImage)
            .FirstOrDefaultAsync(m => m.MemberID == id && !m.IsDeleted);
        if (member == null) return NotFound();

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
            // 5.3 允許編輯欄位（帳號 UserName 不可變更，故不接受前端傳入值覆寫）
            memberInDb.NickName = model.NickName;
            memberInDb.Status = model.Status;
            memberInDb.AdminNote = model.AdminNote;

            // 5.5 PenaltyEndAt 處分期限計算
            if (model.Status == "Normal" || model.Status == "Deleted")
            {
                memberInDb.PenaltyEndAt = null;
            }
            else
            {
                memberInDb.PenaltyEndAt = model.PenaltyDays switch
                {
                    "1" => DateTime.Now.AddDays(1),
                    "3" => DateTime.Now.AddDays(3),
                    "7" => DateTime.Now.AddDays(7),
                    "14" => DateTime.Now.AddDays(14),
                    "30" => DateTime.Now.AddDays(30),
                    "Permanent" => null, // 永久停權為 null
                    _ => memberInDb.PenaltyEndAt
                };
            }

            // 5.6 & 11. Status = Deleted 的連動規則（軟刪除：僅標記，不移除資料列）
            if (model.Status == "Deleted")
            {
                memberInDb.IsDeleted = true;
                memberInDb.DeletedAt = DateTime.Now;
                var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(adminIdClaim, out var adminId))
                {
                    memberInDb.DeletedBy = adminId;
                }
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
            displayMember.NickName = model.NickName;
            displayMember.Status = model.Status;
            displayMember.AdminNote = model.AdminNote;
        }

        ViewBag.ApprovedReports = await _context.Reports
            .Where(r => r.ReportedMemberID == id && r.Status == "Approved")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        ViewBag.Levels = await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync();
        return View(displayMember);
    }

    // 5.3 會員照片移除（僅能移除，改回預設照片；需填寫原因）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar(int id, string reason)
    {
        var memberInDb = await _context.Members.FirstOrDefaultAsync(m => m.MemberID == id && !m.IsDeleted);
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
}