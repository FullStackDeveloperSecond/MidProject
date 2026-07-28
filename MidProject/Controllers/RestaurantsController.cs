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
            // 這個分支回傳的是不含 _Layout（沒有 <head>/CSS）的內容片段，只給前端 fetch()
            // 抓回來塞進 #raContent 用。若沒有 Cache-Control: no-store，瀏覽器在「上一頁/
            // 下一頁」導覽時可能直接沿用同一個網址先前快取到的這份「片段」回應，當成完整頁面
            // 顯示，畫面就會變成完全沒套用樣式的裸 HTML。明確關閉快取，確保之後對同一網址的
            // 真實整頁導覽一定會重新打一次伺服器，走到下面 return View(...) 那條完整頁面路徑。
            Response.Headers.CacheControl = "no-store";
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
            Response.Headers.CacheControl = "no-store";
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

        if (!TryGetAdminId(out var adminId))
        {
            return Forbid();
        }

        var (success, newId) = await _restaurantService.CreateAsync(form, adminId);
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

        if (!TryGetAdminId(out var adminId))
        {
            return Forbid();
        }

        var success = await _restaurantService.EditAsync(id, form, adminId);
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
        if (!TryGetAdminId(out var adminId))
        {
            return Forbid();
        }

        var success = await _restaurantService.DisableAsync(id, reason, adminId);
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

    private bool TryGetAdminId(out int adminId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out adminId) && adminId > 0;
    }
}
