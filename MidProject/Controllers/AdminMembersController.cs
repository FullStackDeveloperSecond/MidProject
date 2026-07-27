using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidProject.Models.ViewModels;
using MidProject.Services.IServices;
using System.Security.Claims;

// Controller 只呼叫 IMemberService / IMemberEscalationService，不直接持有 AppDbContext
// （Controller → Service → Repository → DbContext）。
[Authorize(Roles = "Admin")]
public class AdminMembersController : Controller
{
    private readonly IMemberService _memberService;
    private readonly IMemberEscalationService _escalationService;

    public AdminMembersController(IMemberService memberService, IMemberEscalationService escalationService)
    {
        _memberService = memberService;
        _escalationService = escalationService;
    }

    // 5.5 立即重新檢查懲處（POST，管理員手動觸發）：跟排程背景服務共用同一份 IMemberEscalationService，
    // 不是另一套邏輯。明確的使用者動作才會寫資料庫，跟 GET 頁面瀏覽脫鉤。
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalculateEscalation(int? returnToMemberId)
    {
        await _escalationService.RunOnceAsync();
        TempData["EscalationResult"] = "已重新整理。";

        return returnToMemberId.HasValue
            ? RedirectToAction(nameof(Edit), new { id = returnToMemberId.Value })
            : RedirectToAction(nameof(Index));
    }

    // 4. 會員列表頁
    // 純讀取，不寫入資料庫：自動懲處計算已改由 MemberEscalationBackgroundService 排程批次處理，
    // 這裡看到的 Status / PenaltyEndAt 是該排程上次執行後的結果，不會因為開這個頁面而被修改。
    public async Task<IActionResult> Index(string keyword, string statusFilter, int? levelFilter, string sortBy, bool showAbnormal = false, bool todayOnly = false, int page = 1)
    {
        var query = new MemberIndexQuery(keyword, statusFilter, levelFilter, sortBy, showAbnormal, todayOnly, page);
        var data = await _memberService.GetIndexViewDataAsync(query);

        const int pageSize = 10;

        // 狀態保留
        ViewBag.TotalMembers = data.TotalMembers;
        ViewBag.TodayRegistered = data.TodayRegistered;
        ViewBag.AbnormalCount = data.AbnormalCount;
        ViewBag.Keyword = keyword;
        ViewBag.StatusFilter = statusFilter;
        ViewBag.LevelFilter = levelFilter;
        ViewBag.SortBy = sortBy;
        ViewBag.ShowAbnormal = showAbnormal;
        ViewBag.TodayOnly = todayOnly;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)data.TotalItems / pageSize);
        ViewBag.Levels = data.Levels; // 供篩選下拉選單與經驗值進度使用

        return View(data.Members);
    }

    // 5. 會員詳細 / 編輯頁 (GET)
    // 純讀取，不寫入資料庫：自動懲處計算已改由 MemberEscalationBackgroundService 排程批次處理（見該檔案）。
    public async Task<IActionResult> Edit(int id)
    {
        var data = await _memberService.GetEditViewDataAsync(id);
        if (data == null) return NotFound();

        ApplyEditViewBag(data);
        return View(data.Member);
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

        var adminIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(adminIdClaim, out var currentAdminId) || currentAdminId <= 0)
        {
            return Forbid();
        }

        var currentAdminName = User.FindFirstValue(ClaimTypes.Name) ?? "管理員";
        var memberEditOperator = new MemberEditOperator(currentAdminId, currentAdminName);

        var outcome = await _memberService.SaveMemberEditAsync(id, model, memberEditOperator);

        if (outcome.Kind == MemberEditOutcomeKind.NotFound)
        {
            return NotFound();
        }
        if (outcome.Kind == MemberEditOutcomeKind.Success)
        {
            return RedirectToAction(nameof(Index));
        }

        foreach (var (field, message) in outcome.ValidationErrors!)
        {
            ModelState.AddModelError(field, message);
        }

        ApplyEditViewBag(outcome.RedisplayData!);
        return View(outcome.RedisplayData!.Member);
    }

    // GET／POST 驗證失敗兩種情況共用同一份 ViewBag 映射，確保 Edit.cshtml 讀到的欄位一致
    private void ApplyEditViewBag(MemberEditViewData data)
    {
        ViewBag.ApprovedReports = data.ApprovedReports;
        ViewBag.Levels = data.Levels;
        ViewBag.OriginalStatus = data.OriginalStatus;
        ViewBag.OriginalNickName = data.OriginalNickName;
        ViewBag.OriginalPoints = data.OriginalPoints;
        ViewBag.StatusChangeReason = data.StatusChangeReason;
        ViewBag.NicknameChangeReason = data.NicknameChangeReason;
        ViewBag.RemoveAvatarRequested = data.RemoveAvatarRequested;
        ViewBag.AvatarRemovalReason = data.AvatarRemovalReason;
        ViewBag.PointsChangeReason = data.PointsChangeReason;
        ViewBag.UnlockAccountRequested = data.UnlockAccountRequested;
        ViewBag.UnlockReason = data.UnlockReason;
    }
}
