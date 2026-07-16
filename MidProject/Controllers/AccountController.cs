using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
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
        using (var command = _context.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = "SELECT MemberID, UserName, Email, PasswordHash, Role, IsDeleted FROM Members WHERE Email = @Email AND Role = 'Admin' AND IsDeleted = 0";

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

        // 💡 防禦性檢查：如果資料庫完全找不到這筆 Admin 資料
        if (admin == null)
        {
            ModelState.AddModelError("", "帳號或密碼錯誤，或您無管理權限。");
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

        return RedirectToAction("Index", "AdminMembers");
    }

    // 登出 Action
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }


}