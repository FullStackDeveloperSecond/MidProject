using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidProject.Models.DTOs;
using MidProject.Services;
using System.Security.Claims;

namespace MidProject.Controllers;

// 後台檢舉管理僅限管理員：匿名與一般 User 一律擋下（重導登入 / 拒絕存取）
[Authorize(Roles = "Admin")]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // GET: /Reports?Status=Pending&TargetType=Restaurant&Page=1
    public async Task<IActionResult> Index(ReportQueryParams query)
    {
        var result = await _reportService.GetReportsAsync(query);
        ViewBag.CurrentStatus = query.Status;
        ViewBag.CurrentTargetType = query.TargetType;
        ViewBag.CurrentCategory = query.Category;
        ViewBag.CurrentSortBy = query.SortBy;
        ViewBag.CurrentSortDirection = query.SortDirection;
        return View(result);
    }

    // GET: /Reports/Dashboard
    public async Task<IActionResult> Dashboard()
    {
        var dashboard = await _reportService.GetDashboardAsync();
        return View(dashboard);
    }

    // GET: /Reports/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var report = await _reportService.GetByIdAsync(id);
        if (report == null) return NotFound();
        return View(report);
    }

    // POST: /Reports/Handle/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Handle(int id, ReportHandleDto dto)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "資料驗證失敗" });

            var report = await _reportService.GetByIdAsync(id);
            return View("Details", report);
        }

        var adminMemberId = GetCurrentAdminMemberId();

        // 直接處理（非 AJAX 後備路徑）：實際流程改由通知視窗按「儲存」時定案，此處僅作後備。
        var outcome = await _reportService.HandleReportAsync(id, dto, adminMemberId);
        var message = outcome switch
        {
            ReportHandleOutcome.Handled => "檢舉已處理。",
            ReportHandleOutcome.AlreadyHandled => "此檢舉已由其他管理員處理，請重新整理後確認。",
            ReportHandleOutcome.InvalidStatus => "處理結果不正確（僅能為檢舉成立或駁回檢舉）。",
            ReportHandleOutcome.AdminNoteRequired => "處理檢舉時「管理員備註」為必填。",
            ReportHandleOutcome.AdminNoteTooLong => "「管理員備註」最多 30 字。",
            _ => "找不到指定的檢舉。"
        };

        if (IsAjaxRequest())
            return Json(new { success = outcome == ReportHandleOutcome.Handled, message });

        TempData["Message"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Reports/NotifyReporter/5 — 管理員按「儲存」交由通知模組通知檢舉者（建立未發送通知）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotifyReporter(int id, NotifyReporterDto dto)
    {
        var result = await _reportService.NotifyReporterAsync(id, dto, GetCurrentAdminMemberId());
        if (IsAjaxRequest())
            return Json(new { success = result.Success, alreadyNotified = result.AlreadyNotified, recipientUnavailable = result.RecipientUnavailable, message = result.Message });

        TempData["Message"] = result.Success ? "已交由通知模組通知檢舉者。" : (result.Message ?? "通知失敗。");
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Reports/NotifyReportedMember/5 — 通知被檢舉會員（僅檢舉成立）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotifyReportedMember(int id, NotifyReporterDto dto)
    {
        var result = await _reportService.NotifyReportedMemberAsync(id, dto, GetCurrentAdminMemberId());
        if (IsAjaxRequest())
            return Json(new { success = result.Success, alreadyNotified = result.AlreadyNotified, recipientUnavailable = result.RecipientUnavailable, message = result.Message });

        TempData["Message"] = result.Success ? "已交由通知模組通知被檢舉會員。" : (result.Message ?? "通知失敗。");
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: /Reports/SentNotifications/5
    // 「通知紀錄」視窗用：查詢該檢舉的通知（依 Notifications.SourceReportID 關聯，含未發送）
    [HttpGet]
    public async Task<IActionResult> SentNotifications(int id)
    {
        var records = await _reportService.GetSentNotificationsAsync(id);
        return Json(new { success = true, records });
    }

    // 從登入憑證取得目前管理員的 MemberID（登入時寫入 ClaimTypes.NameIdentifier）
    private int GetCurrentAdminMemberId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idValue, out var memberId) ? memberId : 0;
    }

    private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
}
