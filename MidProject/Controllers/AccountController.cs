using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels;
using MidProject.Services;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

public class AccountController : Controller
{
    private readonly AppDbContext _context; // 請確認這裡與你的 DbContext 名稱一致

    public AccountController(AppDbContext context)
    {
        _context = context;
    }

    // 6.1 登入網頁 (GET: /Account/Login)
    [HttpGet]
    public IActionResult Login()
    {
        // 如果已經是登入狀態，直接送他去會員管理列表
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Index", "AdminMembers");
        }
        return View();
    }

    // 6.1 執行登入驗證 (POST: /Account/Login)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        // 8. 基礎欄位驗證
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError("", "Email 與密碼為必填。");
            return View();
        }

        Member admin = null;

        // 💡 改用 ADO.NET 原生 SQL 查詢，繞過 EF Core 的 Entity 對應錯誤
        // 6.1 後台 Admin 與一般 User 皆可登入，登入後依角色導向不同頁面
        using (var command = _context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT MemberID, UserName, Email, PasswordHash, Role, IsDeleted FROM Members WHERE Email = @Email AND IsDeleted = 0";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@Email";
            parameter.Value = email ?? (object)DBNull.Value;
            command.Parameters.Add(parameter);

            if (_context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            {
                await _context.Database.GetDbConnection().OpenAsync();
            }

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    admin = new Member
                    {
                        MemberID = reader.GetInt32(0),
                        UserName = reader.GetString(1),
                        Email = reader.GetString(2),
                        PasswordHash = reader.GetString(3),
                        Role = reader.GetString(4),
                        IsDeleted = reader.GetBoolean(5)
                    };
                }
            }
        }

        // 💡 防禦性檢查：如果資料庫完全找不到這筆帳號資料
        if (admin == null)
        {
            ModelState.AddModelError("", "帳號或密碼錯誤。");
            return View();
        }

        // 6.3 密碼驗證 (使用 PasswordHash)
        bool isPasswordValid = PasswordHashService.VerifyPassword(password, admin.PasswordHash);

        if (!isPasswordValid)
        {
            ModelState.AddModelError("", "帳號或密碼錯誤。");
            return View();
        }

        // 驗證通過！建立使用者的身份憑證 (Claims)
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, admin.MemberID.ToString()),
        new Claim(ClaimTypes.Name, admin.UserName),
        new Claim(ClaimTypes.Role, admin.Role)
    };

        var claimsIdentity = new ClaimsIdentity(claims, Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties { IsPersistent = true };

        await HttpContext.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

        // 登入後依角色導向：Admin 進後台會員管理，一般 User 導回首頁
        return admin.Role == "Admin"
            ? RedirectToAction("Index", "AdminMembers")
            : RedirectToAction("Index", "Home");
    }

    // 登出 Action
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    // 一般會員註冊 (GET: /Account/Register)
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "AdminMembers");
        return View();
    }

    // 一般會員註冊 (POST: /Account/Register)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVM model)
    {
        if (!ModelState.IsValid) return View(model);

        if (await _context.Members.AnyAsync(m => m.UserName == model.UserName))
        {
            ModelState.AddModelError(nameof(model.UserName), "此帳號已被使用。");
            return View(model);
        }
        if (await _context.Members.AnyAsync(m => m.Email == model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "此 Email 已被註冊。");
            return View(model);
        }

        var member = new Member
        {
            UserName = model.UserName,
            NickName = string.IsNullOrWhiteSpace(model.NickName) ? model.UserName : model.NickName,
            Email = model.Email,
            PasswordHash = PasswordHashService.HashPassword(model.Password),
            Role = "User",
            Status = "Normal",
            LevelID = 1
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        TempData["RegisterSuccess"] = "註冊成功，請登入。";
        return RedirectToAction(nameof(Login));
    }

    // 後台管理員註冊 (GET: /Account/RegisterAdmin) — 測試/開發用途，公開頁面
    [HttpGet]
    public IActionResult RegisterAdmin()
    {
        if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "AdminMembers");
        return View();
    }

    // 後台管理員註冊 (POST: /Account/RegisterAdmin)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterAdmin(RegisterVM model)
    {
        if (!ModelState.IsValid) return View(model);

        if (await _context.Members.AnyAsync(m => m.UserName == model.UserName))
        {
            ModelState.AddModelError(nameof(model.UserName), "此帳號已被使用。");
            return View(model);
        }
        if (await _context.Members.AnyAsync(m => m.Email == model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "此 Email 已被註冊。");
            return View(model);
        }

        var member = new Member
        {
            UserName = model.UserName,
            NickName = string.IsNullOrWhiteSpace(model.NickName) ? model.UserName : model.NickName,
            Email = model.Email,
            PasswordHash = PasswordHashService.HashPassword(model.Password),
            Role = "Admin",
            Status = "Normal",
            LevelID = 1
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        TempData["RegisterSuccess"] = "管理員帳號註冊成功，請登入。";
        return RedirectToAction(nameof(Login));
    }
}