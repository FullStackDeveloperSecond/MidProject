using Microsoft.AspNetCore.Mvc;
using MidProject.Models.DTOs;
using MidProject.Services;
using MidProject.Services.IServices;

namespace MidProject.Controllers;

[ServiceFilter(typeof(AdminAuthorizationFilter))]
public class ReportsController : Controller
{
    private readonly IReportService _reportService;
    private readonly ICurrentAdminAccessor _currentAdmin;

    public ReportsController(
        IReportService reportService,
        ICurrentAdminAccessor currentAdmin)
    {
        _reportService = reportService;
        _currentAdmin = currentAdmin;
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
        if (dto.Status == "Approved")
        {
            if (string.IsNullOrWhiteSpace(dto.ReportedMemberNotificationTitle))
                ModelState.AddModelError(nameof(dto.ReportedMemberNotificationTitle), "請輸入通知被檢舉會員的標題");

            if (string.IsNullOrWhiteSpace(dto.ReportedMemberNotificationContent))
                ModelState.AddModelError(nameof(dto.ReportedMemberNotificationContent), "請輸入通知被檢舉會員的內容");
        }

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return BadRequest(new
                {
                    success = false,
                    message = string.Join("、", ModelState.Values
                        .SelectMany(value => value.Errors)
                        .Select(error => error.ErrorMessage)
                        .Where(message => !string.IsNullOrWhiteSpace(message)))
                });

            var report = await _reportService.GetByIdAsync(id);
            return View("Details", report);
        }

        var adminMemberId = _currentAdmin.MemberID;

        var success = await _reportService.HandleReportAsync(id, dto, adminMemberId);
        if (!success) return NotFound();

        if (IsAjaxRequest())
            return Json(new { success = true });

        TempData["Message"] = "檢舉已處理";
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool IsAjaxRequest() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
}
