using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class CurrentAdminAccessor : ICurrentAdminAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentAdminAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int MemberID
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("A current HTTP context is required.");

            return AdminContext.FromHttpContext(httpContext).MemberID;
        }
    }
}
