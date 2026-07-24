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
            command.CommandText = "SELECT MemberID, UserName, NickName, Email, PasswordHash, Role, IsDeleted, IsActive, IsLocked, Status, FailedLoginCount FROM Members WHERE Email = @Email AND IsDeleted = 0";

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
                        NickName = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Email = reader.GetString(3),
                        PasswordHash = reader.GetString(4),
                        Role = reader.GetString(5),
                        IsDeleted = reader.GetBoolean(6),
                        IsActive = reader.GetBoolean(7),
                        IsLocked = reader.GetBoolean(8),
                        Status = reader.GetString(9),
                        FailedLoginCount = reader.GetInt32(10)
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

        // 8. 帳號資格檢查：鎖定／停用／停權的帳號一律拒絕登入（Suspended 目前的慣例是同時 IsDeleted=true，
        // 已經被上面的 SQL 篩掉，這裡仍明確檢查一次，避免未來這個慣例被打破時悄悄放行）
        if (admin.IsLocked)
        {
            ModelState.AddModelError("", "帳號已因密碼輸入錯誤過多次被鎖定，請聯繫管理員解除鎖定。");
            return View();
        }
        if (!admin.IsActive)
        {
            ModelState.AddModelError("", "帳號已停用，請聯繫管理員。");
            return View();
        }
        if (admin.Status == "Suspended")
        {
            ModelState.AddModelError("", "帳號目前為停權狀態，請聯繫管理員。");
            return View();
        }

        // 6.3 密碼驗證 (使用 PasswordHash)
        bool isPasswordValid = PasswordHashService.VerifyPassword(password, admin.PasswordHash);

        if (!isPasswordValid)
        {
            // 8. 連續密碼錯誤達 3 次鎖定帳號，須由管理員在會員編輯頁手動解鎖（AdminMembersController.Edit）
            var failedCount = admin.FailedLoginCount + 1;
            var willLock = failedCount >= 3;
            await _context.Members.Where(m => m.MemberID == admin.MemberID)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.FailedLoginCount, failedCount)
                    .SetProperty(m => m.IsLocked, willLock));

            ModelState.AddModelError("", willLock
                ? "帳號或密碼錯誤，密碼已連續錯誤 3 次，帳號已被鎖定，請聯繫管理員解除鎖定。"
                : "帳號或密碼錯誤。");
            return View();
        }

        // 登入成功：重置失敗次數
        if (admin.FailedLoginCount != 0)
        {
            await _context.Members.Where(m => m.MemberID == admin.MemberID)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.FailedLoginCount, 0));
        }

        // 驗證通過！建立使用者的身份憑證 (Claims)
        // 明確提供 MemberID、Name、Role 三個 claim；其他模組（餐廳/評論/檢舉/通知管理）依賴這個契約做登入與身分辨識
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, admin.MemberID.ToString()),
        new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(admin.NickName) ? admin.UserName : admin.NickName),
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
            Birthday = model.Birthday,
            Role = "User",
            Status = "Normal",
            LevelID = 1
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        TempData["RegisterSuccess"] = "註冊成功，請登入。";
        return RedirectToAction(nameof(Login));
    }

}