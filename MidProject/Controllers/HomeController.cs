using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MidProject.Models;
using MidProject.Services;
using MidProject.Services.IServices;

namespace MidProject.Controllers;

public class HomeController : Controller
{
    private readonly IDashboardService _dashboardService;

    public HomeController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [ServiceFilter(typeof(AdminAuthorizationFilter))]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _dashboardService.GetIndexAsync(cancellationToken));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
