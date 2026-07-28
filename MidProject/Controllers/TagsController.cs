using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MidProject.Services.IServices;
using System.Security.Claims;

namespace MidProject.Controllers;

[Authorize(Roles = "Admin")]
public class TagsController : Controller
{
    private readonly ITagService _tagService;

    public TagsController(ITagService tagService)
    {
        _tagService = tagService;
    }

    // GET /Tags
    public async Task<IActionResult> Index()
    {
        var model = await _tagService.GetIndexAsync();
        return View(model);
    }

    // POST /Tags/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name)
    {
        var (success, error) = await _tagService.CreateAsync(name, GetAdminId());
        TempData["Toast"] = success ? "標籤已新增。" : error;
        return RedirectToAction(nameof(Index));
    }

    // POST /Tags/Toggle/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var success = await _tagService.ToggleAsync(id, GetAdminId());
        if (!success) return NotFound();
        return RedirectToAction(nameof(Index));
    }

    // POST /Tags/Reorder — 標籤牆拖曳排序，回傳新的顯示順序（僅使用中的標籤 ID）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(List<int> orderedIds)
    {
        if (orderedIds == null || orderedIds.Count == 0) return BadRequest();
        await _tagService.ReorderAsync(orderedIds);
        return Ok();
    }

    private int GetAdminId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
}
