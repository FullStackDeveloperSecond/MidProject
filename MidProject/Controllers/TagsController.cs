using Microsoft.AspNetCore.Mvc;
using MidProject.Services.IServices;

namespace MidProject.Controllers;

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
        var (success, error) = await _tagService.CreateAsync(name);
        TempData["Toast"] = success ? "標籤已新增。" : error;
        return RedirectToAction(nameof(Index));
    }

    // POST /Tags/Toggle/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        await _tagService.ToggleAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
