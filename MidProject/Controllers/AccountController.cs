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
        if (User.Identity?.IsAuthenticated == true)
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

        // 6.1 這個網站是後台管理系統，只開放 Admin 角色登入（見 MemberLoginPolicy.CheckEligibility）
        // （原本這裡是繞過 EF、直接用 ADO.NET 讀 Members 表；查證後目前的 EF 對應設定跟 migration
        // 都是同步的，一般 FirstOrDefaultAsync 查詢可以正常運作，沒有理由再手動組 SQL、自己拼實體。）
        var admin = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);

        // 💡 防禦性檢查：如果資料庫完全找不到這筆帳號資料
        if (admin == null)
        {
            ModelState.AddModelError("", "帳號或密碼錯誤。");
            return View();
        }

        // 8. 帳號資格檢查：鎖定／停用／停權／刪除的帳號一律拒絕登入。
        // 必須先取得帳號再交給 policy，否則 Suspended 通常同時 IsDeleted=true，
        // 會在這裡被誤判為查無帳號，永遠無法顯示正確的停權訊息。
        // 規則本身抽到 MemberLoginPolicy（純函式，無 DbContext/HttpContext 依賴），方便單元測試。
        var eligibilityError = MemberLoginPolicy.CheckEligibility(admin);
        if (eligibilityError != null)
        {
            ModelState.AddModelError("", eligibilityError);
            return View();
        }

        // 6.3 密碼驗證 (使用 PasswordHash)
        bool isPasswordValid = PasswordHashService.VerifyPassword(password, admin.PasswordHash);

        if (!isPasswordValid)
        {
            // 8. 連續密碼錯誤達 3 次鎖定帳號，須由管理員在會員編輯頁手動解鎖（AdminMembersController.Edit）
            // 必須讓 SQL Server 直接以資料庫中的目前值遞增，不能先在記憶體算好固定值再覆寫；
            // 否則多個並行的錯誤密碼請求可能都讀到相同次數，造成實際嘗試次數被少算。
            await _context.Members.Where(m => m.MemberID == admin.MemberID)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.FailedLoginCount, m => m.FailedLoginCount + 1)
                    .SetProperty(m => m.IsLocked, m => m.FailedLoginCount + 1 >= MemberLoginPolicy.MaxFailedAttempts));

            var willLock = await _context.Members
                .AsNoTracking()
                .Where(m => m.MemberID == admin.MemberID)
                .Select(m => m.IsLocked)
                .SingleAsync();

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
