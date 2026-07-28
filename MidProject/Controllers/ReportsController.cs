using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidProject.Models.DTOs;
using MidProject.Services;

namespace MidProject.Controllers;

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

        // TODO: 等 Members 模組的登入/驗證機制完成後，改成從登入狀態取得目前管理員 ID
        var adminMemberId = GetCurrentAdminMemberId();

        var success = await _reportService.HandleReportAsync(id, dto, adminMemberId);
        if (!success) return NotFound();

        if (IsAjaxRequest())
        {
            // 處理完成後前端要立刻跳出通知視窗：檢舉成立給「通知檢舉者」+「通知被檢舉會員」兩張卡片，
            // 駁回檢舉只給「通知檢舉者」一張卡片，預設標題／內容直接沿用 ReportDto 既有的範本
            var updated = await _reportService.GetByIdAsync(id);
            return Json(new
            {
                success = true,
                status = updated!.Status,
                reporter = new { title = updated.DefaultNotificationTitle, content = updated.DefaultNotificationContent },
                reportedMember = updated.Status == "Approved"
                    ? new { title = updated.DefaultReportedMemberNotificationTitle, content = updated.DefaultReportedMemberNotificationContent }
                    : null
            });
        }

        TempData["Message"] = "檢舉已處理";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Reports/NotifyReporter/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotifyReporter(int id, NotifyReporterDto dto)
    {
        var adminMemberId = GetCurrentAdminMemberId();

        var success = await _reportService.NotifyReporterAsync(id, dto, adminMemberId);
        if (!success) return NotFound();

        if (IsAjaxRequest())
            return Json(new { success = true });

        TempData["Message"] = "已通知檢舉會員審核結果";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST: /Reports/NotifyReportedMember/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotifyReportedMember(int id, NotifyReporterDto dto)
    {
        var adminMemberId = GetCurrentAdminMemberId();

        var success = await _reportService.NotifyReportedMemberAsync(id, dto, adminMemberId);
        if (!success) return NotFound();

        if (IsAjaxRequest())
            return Json(new { success = true });

        TempData["Message"] = "已通知被檢舉會員";
        return RedirectToAction(nameof(Details), new { id });
    }

    private int GetCurrentAdminMemberId()
    {
        // 暫時寫死，之後接上登入驗證後從 HttpContext.User 或 Session 取得
        return 1;
    }

    private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
}
