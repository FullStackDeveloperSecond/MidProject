using Microsoft.AspNetCore.Mvc;
using MidProject.Models.DTOs;
using MidProject.Services;

namespace MidProject.Controllers;

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
            var report = await _reportService.GetByIdAsync(id);
            return View("Details", report);
        }

        // TODO: 等 Members 模組的登入/驗證機制完成後，改成從登入狀態取得目前管理員 ID
        var adminMemberId = GetCurrentAdminMemberId();

        var success = await _reportService.HandleReportAsync(id, dto, adminMemberId);
        if (!success) return NotFound();

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

        TempData["Message"] = "已通知檢舉會員審核結果";
        return RedirectToAction(nameof(Details), new { id });
    }

    private int GetCurrentAdminMemberId()
    {
        // 暫時寫死，之後接上登入驗證後從 HttpContext.User 或 Session 取得
        return 1;
    }
}
