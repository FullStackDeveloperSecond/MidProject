using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MidProject.Data;

namespace MidProject.Services;

/// <summary>
/// Cookie 內的 Admin role 只代表登入當下的狀態。每次使用 Cookie 時重新確認會員資料，
/// 讓停權、刪除、停用或鎖定可以立即撤銷既有工作階段。
/// </summary>
public sealed class AdminCookieAuthenticationEvents : CookieAuthenticationEvents
{
    private readonly AppDbContext _context;

    public AdminCookieAuthenticationEvents(AppDbContext context)
    {
        _context = context;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var memberIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var isEligibleAdmin = int.TryParse(memberIdClaim, out var memberId)
            && memberId > 0
            && await _context.Members.AsNoTracking().AnyAsync(
                member => member.MemberID == memberId
                    && member.Role == "Admin"
                    && member.IsActive
                    && !member.IsLocked
                    && !member.IsDeleted
                    && member.Status != "Suspended"
                    && member.Status != "Deleted",
                context.HttpContext.RequestAborted);

        if (isEligibleAdmin)
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
