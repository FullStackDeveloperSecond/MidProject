using Microsoft.AspNetCore.Mvc;
using MidProject.Services.IServices;

namespace MidProject.Controllers;

public sealed class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<IActionResult> Details(string type, CancellationToken cancellationToken)
    {
        var model = await _dashboardService.GetDetailsAsync(type, cancellationToken);
        return model is null ? NotFound() : View(model);
    }
}
