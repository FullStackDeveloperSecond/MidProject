using System.Security.Claims;
using MidProject.Services.IServices;

namespace MidProject.Services;

// AccountLogin 模式下的正式實作：直接讀取 AccountController.Login 簽發的 Cookie Claims
// （ClaimTypes.NameIdentifier = MemberID），取代 DevelopmentTemporaryTrustedMemberIdentityAccessor 那組
// 寫死單一 demo 帳號的暫時實作。是否有效（Role/Status/IsActive/IsLocked/IsDeleted）交給
// NotificationAdminAccessEvaluator 依 MemberID 重新查資料庫驗證，這裡只單純回報「目前登入者是誰」。
public sealed class CookieClaimsTrustedMemberIdentityAccessor : ITrustedMemberIdentityAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieClaimsTrustedMemberIdentityAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask<TrustedMemberIdentity> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return ValueTask.FromResult(new TrustedMemberIdentity(false, null));
        }

        var memberIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(memberIdClaim, out var memberId) || memberId <= 0)
        {
            // fail-closed：Claim 缺失或格式不對一律視為未登入，不猜測身分
            return ValueTask.FromResult(new TrustedMemberIdentity(false, null));
        }

        return ValueTask.FromResult(new TrustedMemberIdentity(true, memberId));
    }
}
