using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidProject.Models.ViewModels.Restaurants;
using MidProject.Services.IServices;
using System.Security.Claims;

namespace MidProject.Controllers;

[Authorize(Roles = "Admin")]
public class RestaurantsController : Controller
{
    private readonly IRestaurantService _restaurantService;

    public RestaurantsController(IRestaurantService restaurantService)
    {
        _restaurantService = restaurantService;
    }

    // GET /Restaurants
    public async Task<IActionResult> Index([FromQuery] RestaurantFilterQuery filter)
    {
        var model = await _restaurantService.GetIndexAsync(filter);
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_IndexContent", model);
        }
        return View(model);
    }

    // GET /Restaurants/Deleted
    public async Task<IActionResult> Deleted([FromQuery] RestaurantDeletedFilterQuery filter)
    {
        var model = await _restaurantService.GetDeletedIndexAsync(filter);
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_DeletedContent", model);
        }
        return View(model);
    }

    // GET /Restaurants/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var model = await _restaurantService.GetDetailAsync(id);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    // GET /Restaurants/CreateForm  (AJAX modal content)
    public async Task<IActionResult> CreateForm()
    {
        var model = await _restaurantService.GetCreateFormAsync();
        return PartialView("_FormPartial", model);
    }

    // GET /Restaurants/EditForm/5  (AJAX modal content)
    public async Task<IActionResult> EditForm(int id)
    {
        var model = await _restaurantService.GetEditFormAsync(id);
        if (model == null)
        {
            return NotFound();
        }

        return PartialView("_FormPartial", model);
    }

    // POST /Restaurants/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RestaurantFormViewModel form)
    {
        form.Hours = BusinessHoursJsonHelper.FromJson(form.HoursJson);

        if (form.SelectedTagIds.Count == 0)
        {
            ModelState.AddModelError(nameof(form.SelectedTagIds), "請至少選擇一個標籤。");
        }

        if (!ModelState.IsValid)
        {
            form = await _restaurantService.RehydrateFormAsync(form);
            return PartialView("_FormPartial", form);
        }

        var (success, newId) = await _restaurantService.CreateAsync(form, GetAdminId());
        if (!success)
        {
            form = await _restaurantService.RehydrateFormAsync(form);
            return PartialView("_FormPartial", form);
        }

        return Json(new { success = true, redirectUrl = Url.Action("Details", new { id = newId }) });
    }

    // POST /Restaurants/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RestaurantFormViewModel form)
    {
        form.Id = id;
        form.Hours = BusinessHoursJsonHelper.FromJson(form.HoursJson);

        if (form.SelectedTagIds.Count == 0)
        {
            ModelState.AddModelError(nameof(form.SelectedTagIds), "請至少選擇一個標籤。");
        }

        if (!ModelState.IsValid)
        {
            form = await _restaurantService.RehydrateFormAsync(form);
            return PartialView("_FormPartial", form);
        }

        var success = await _restaurantService.EditAsync(id, form, GetAdminId());
        if (!success)
        {
            form = await _restaurantService.RehydrateFormAsync(form);
            return PartialView("_FormPartial", form);
        }

        return Json(new { success = true, redirectUrl = Url.Action("Details", new { id }) });
    }

    // POST /Restaurants/Disable/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(int id, string reason)
    {
        var success = await _restaurantService.DisableAsync(id, reason, GetAdminId());
        if (!success) return NotFound();
        TempData["Toast"] = "餐廳已移至停用餐廳一覽。";
        return RedirectToAction(nameof(Deleted));
    }

    // POST /Restaurants/Restore/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var success = await _restaurantService.RestoreAsync(id);
        if (!success) return NotFound();
        TempData["Toast"] = "已解除停用。";
        return RedirectToAction(nameof(Index));
    }

    private int GetAdminId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
