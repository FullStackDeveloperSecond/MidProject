using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidProject.Models.ViewModels.PointsStore;
using MidProject.Services.IServices;
using System.Security.Claims;

namespace MidProject.Controllers;

[Authorize(Roles = "Admin")]
public class AvatarFramesController : Controller
{
    private readonly IAvatarFrameService _avatarFrameService;

    public AvatarFramesController(IAvatarFrameService avatarFrameService)
    {
        _avatarFrameService = avatarFrameService;
    }

    public async Task<IActionResult> Index(string? keyword, string? rarity, bool? isActive, string? sortBy, int page = 1)
    {
        var model = await _avatarFrameService.GetIndexAsync(keyword, rarity, isActive, sortBy, page);
        return View(model);
    }

    public async Task<IActionResult> Deleted(string? keyword, string? rarity, string? sortBy, int page = 1)
    {
        var model = await _avatarFrameService.GetDeletedIndexAsync(keyword, rarity, sortBy, page);
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new AvatarFrameFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AvatarFrameFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetAdminId(out var adminId))
        {
            return Forbid();
        }

        var (success, error) = await _avatarFrameService.CreateAsync(model, adminId);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "新增失敗，請重試。");
            return View(model);
        }

        TempData["Toast"] = "商品新增成功。";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var model = await _avatarFrameService.GetForEditAsync(id);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AvatarFrameFormViewModel model)
    {
        if (id != model.FrameID)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetAdminId(out var adminId))
        {
            return Forbid();
        }

        var (success, error) = await _avatarFrameService.UpdateAsync(id, model, adminId);
        if (!success)
        {
            ModelState.AddModelError(string.Empty, error ?? "更新失敗，請重試。");
            return View(model);
        }

        TempData["Toast"] = "商品已更新。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var success = await _avatarFrameService.ToggleActiveAsync(id);
        if (!success)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Forbid();
        }

        var success = await _avatarFrameService.DeleteAsync(id, adminId);
        if (!success)
        {
            return NotFound();
        }

        TempData["Toast"] = "商品已刪除，可到「已刪除商品」頁面復原。";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id)
    {
        var success = await _avatarFrameService.RestoreAsync(id);
        if (!success)
        {
            return NotFound();
        }

        TempData["Toast"] = "商品已復原，狀態為下架，請視需要重新上架。";
        return RedirectToAction(nameof(Deleted));
    }

    private bool TryGetAdminId(out int adminId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out adminId) && adminId > 0;
    }
}
