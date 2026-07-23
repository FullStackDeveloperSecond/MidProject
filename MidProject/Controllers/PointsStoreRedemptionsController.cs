using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidProject.Services.IServices;

namespace MidProject.Controllers;

[Authorize(Roles = "Admin")]
public class PointsStoreRedemptionsController : Controller
{
    private readonly IPointsStoreRedemptionService _redemptionService;

    public PointsStoreRedemptionsController(IPointsStoreRedemptionService redemptionService)
    {
        _redemptionService = redemptionService;
    }

    public async Task<IActionResult> Index(
        string? keyword,
        int? frameFilter,
        DateOnly? startDate,
        DateOnly? endDate,
        int page = 1)
    {
        var model = await _redemptionService.GetIndexAsync(keyword, frameFilter, startDate, endDate, page);
        return View(model);
    }
}
