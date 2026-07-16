using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;

[Authorize(Roles = "Admin")]
public class UserLevelsController : Controller
{
    private readonly AppDbContext _context;

    public UserLevelsController(AppDbContext context)
    {
        _context = context;
    }

    // 7. 會員等級列表
    public async Task<IActionResult> Index()
    {
        var levels = await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync();
        return View(levels);
    }

    // 建立會員等級 (GET)
    public IActionResult Create()
    {
        return View();
    }

    // 建立會員等級 (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserLevel model)
    {
        if (ModelState.IsValid)
        {
            _context.UserLevels.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    // 編輯會員等級 (GET)
    public async Task<IActionResult> Edit(int id)
    {
        var level = await _context.UserLevels.FindAsync(id);
        if (level == null) return NotFound();
        return View(level);
    }

    // 編輯會員等級 (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UserLevel model)
    {
        if (id != model.LevelID) return NotFound();

        if (ModelState.IsValid)
        {
            var levelInDb = await _context.UserLevels.FindAsync(id);
            if (levelInDb == null) return NotFound();

            levelInDb.LevelName = model.LevelName;
            levelInDb.MinExp = model.MinExp;
            levelInDb.Rewards = model.Rewards;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }
}
