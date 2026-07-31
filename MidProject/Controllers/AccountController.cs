using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels;
using MidProject.Services;
using MidProject.Services.IServices;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

public class AccountController : Controller
{
    private const string GenericLoginError = "帳號或密碼錯誤。";
    private readonly AppDbContext _context;
    private readonly ITaipeiClock _clock;

    public AccountController(AppDbContext context, ITaipeiClock clock)
    {
        _context = context;
        _clock = clock;
    }

    // 6.1 登入網頁 (GET: /Account/Login)
    [HttpGet]
    public IActionResult Login()
    {
        // 如果已經是登入狀態，直接送他去會員管理列表
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "AdminMembers");
        }
        return View();
    }

    // 6.1 執行登入驗證 (POST: /Account/Login)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string email, string password)
    {
        // 8. 基礎欄位驗證
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError("", "Email 與密碼為必填。");
            return View();
        }

        // 6.1 這個網站是後台管理系統，只開放 Admin 角色登入（見 MemberLoginPolicy.CheckEligibility）
        // （原本這裡是繞過 EF、直接用 ADO.NET 讀 Members 表；查證後目前的 EF 對應設定跟 migration
        // 都是同步的，一般 FirstOrDefaultAsync 查詢可以正常運作，沒有理由再手動組 SQL、自己拼實體。）
        var admin = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);

        // 💡 防禦性檢查：如果資料庫完全找不到這筆帳號資料
        if (admin == null)
        {
            ModelState.AddModelError("", GenericLoginError);
            return View();
        }

        var now = _clock.GetNow();
        if (MemberLoginPolicy.HasExpiredLoginLockout(admin, now))
        {
            admin.FailedLoginCount = 0;
            admin.IsLocked = false;
            admin.LoginLockoutEndAt = null;
            admin.UpdatedAt = now;
            await _context.SaveChangesAsync();
        }

        // 先攔截鎖定、停用、停權與刪除狀態，但角色限制延後到密碼驗證成功後。
        // 這可確保一般會員輸入錯誤密碼時也會寫入 FailedLoginCount。
        var accountStateError = MemberLoginPolicy.CheckAccountState(admin);
        if (accountStateError != null)
        {
            ModelState.AddModelError(
                "",
                admin.IsLocked
                    ? MemberLoginPolicy.GetLockoutMessage(admin.LoginLockoutEndAt)
                    : GenericLoginError);
            return View();
        }

        // 6.3 密碼驗證 (使用 PasswordHash)
        bool isPasswordValid = PasswordHashService.VerifyPassword(password, admin.PasswordHash);

        if (!isPasswordValid)
        {
            // 每次錯誤都透過追蹤實體與 SaveChanges 寫入 Members.FailedLoginCount；
            // 第三次同一筆更新會一併寫入鎖定狀態與到期時間。
            var (failedLoginCount, shouldLock) =
                MemberLoginPolicy.RecordFailedAttempt(admin.FailedLoginCount);
            admin.FailedLoginCount = failedLoginCount;
            admin.UpdatedAt = now;
            var loginError = GenericLoginError;
            if (shouldLock)
            {
                var lockoutEnd = now.Add(MemberLoginPolicy.LoginLockoutDuration);
                admin.IsLocked = true;
                admin.LoginLockoutEndAt = lockoutEnd;
                loginError = MemberLoginPolicy.GetLockoutMessage(lockoutEnd);
            }

            await _context.SaveChangesAsync();
            ModelState.AddModelError("", loginError);
            return View();
        }

        // 密碼正確後才判斷是否具有後台角色，避免角色檢查略過錯誤密碼累計。
        var eligibilityError = MemberLoginPolicy.CheckEligibility(admin);
        if (eligibilityError != null)
        {
            ModelState.AddModelError("", GenericLoginError);
            return View();
        }

        // 登入成功：重置失敗次數
        if (admin.FailedLoginCount != 0)
        {
            admin.FailedLoginCount = 0;
            admin.IsLocked = false;
            admin.LoginLockoutEndAt = null;
            admin.UpdatedAt = now;
            await _context.SaveChangesAsync();
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

        // 走到這裡一定是 Admin（MemberLoginPolicy.CheckEligibility 已經擋掉非 Admin），直接進後台會員管理
        return RedirectToAction("Index", "AdminMembers");
    }

    // 登出 Action：改用 POST + Anti-forgery token（登出會改變登入狀態，不該用 GET 就能觸發，
    // 否則外部網頁只要放一個 <img src="/Account/Logout"> 就能強制使用者登出，屬於 CSRF 風險）
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View(
            "~/Views/Shared/AdminAccessDenied.cshtml",
            new AdminAccessDeniedViewModel
            {
                Message = "您沒有權限存取後台管理功能。"
            });
    }

    // 一般會員註冊 (GET: /Account/Register)
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "AdminMembers");
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
