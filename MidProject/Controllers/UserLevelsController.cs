using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels;
using MidProject.Services;

[ServiceFilter(typeof(AdminAuthorizationFilter))]
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
        return View(new UserLevelFormViewModel());
    }

    // 建立會員等級 (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserLevelFormViewModel model)
    {
        Normalize(model);
        if (string.IsNullOrWhiteSpace(model.LevelName))
        {
            ModelState.AddModelError(nameof(model.LevelName), "請輸入等級名稱。");
        }
        if (!ModelState.IsValid || !await ValidateUniqueFieldsAsync(model))
        {
            return View(model);
        }

        _context.UserLevels.Add(new UserLevel
        {
            LevelName = model.LevelName,
            MinExp = model.MinExp,
            Rewards = model.Rewards,
            IsDeleted = false
        });

        try
        {
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "等級名稱或最低經驗值已被使用，請重新確認。");
            return View(model);
        }
    }

    // 編輯會員等級 (GET)
    public async Task<IActionResult> Edit(int id)
    {
        var level = await _context.UserLevels.FindAsync(id);
        if (level == null) return NotFound();
        return View(new UserLevelFormViewModel
        {
            LevelID = level.LevelID,
            LevelName = level.LevelName,
            MinExp = level.MinExp,
            Rewards = level.Rewards
        });
    }

    // 編輯會員等級 (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UserLevelFormViewModel model)
    {
        if (id != model.LevelID) return NotFound();

        Normalize(model);
        if (string.IsNullOrWhiteSpace(model.LevelName))
        {
            ModelState.AddModelError(nameof(model.LevelName), "請輸入等級名稱。");
        }
        if (!ModelState.IsValid || !await ValidateUniqueFieldsAsync(model, id))
        {
            return View(model);
        }

        var levelInDb = await _context.UserLevels.FindAsync(id);
        if (levelInDb == null) return NotFound();

        levelInDb.LevelName = model.LevelName;
        levelInDb.MinExp = model.MinExp;
        levelInDb.Rewards = model.Rewards;

        try
        {
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "等級名稱或最低經驗值已被使用，請重新確認。");
            return View(model);
        }
    }

    private static void Normalize(UserLevelFormViewModel model)
    {
        model.LevelName = model.LevelName?.Trim() ?? string.Empty;
        model.Rewards = string.IsNullOrWhiteSpace(model.Rewards) ? null : model.Rewards.Trim();
    }

    private async Task<bool> ValidateUniqueFieldsAsync(UserLevelFormViewModel model, int? excludedLevelId = null)
    {
        var query = _context.UserLevels.AsNoTracking();
        if (excludedLevelId.HasValue)
        {
            query = query.Where(level => level.LevelID != excludedLevelId.Value);
        }

        var duplicateName = await query.AnyAsync(level => level.LevelName == model.LevelName);
        if (duplicateName)
        {
            ModelState.AddModelError(nameof(model.LevelName), "此等級名稱已存在。");
        }

        var duplicateMinExp = await query.AnyAsync(level => level.MinExp == model.MinExp);
        if (duplicateMinExp)
        {
            ModelState.AddModelError(nameof(model.MinExp), "此最低經驗值已被其他等級使用。");
        }

        return !duplicateName && !duplicateMinExp;
    }
}
