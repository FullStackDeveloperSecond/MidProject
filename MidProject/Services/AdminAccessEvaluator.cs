using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class AdminAccessEvaluator : IAdminAccessEvaluator
{
    private readonly AppDbContext _dbContext;

    public AdminAccessEvaluator(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminAccessResult> EvaluateAsync(
        int? memberId,
        CancellationToken cancellationToken = default)
    {
        if (!memberId.HasValue || memberId.Value <= 0)
        {
            return new(AdminAccessClassification.IdentityUnmapped);
        }

        var member = await _dbContext.Members.AsNoTracking()
            .Where(x => x.MemberID == memberId.Value)
            .Select(x => new { x.MemberID, x.Role, x.Status, x.IsActive, x.IsLocked, x.IsDeleted })
            .SingleOrDefaultAsync(cancellationToken);

        if (member is null || member.IsDeleted || !member.IsActive || member.IsLocked || member.Status != "Normal")
        {
            return new(AdminAccessClassification.AdministratorUnavailable);
        }

        if (member.Role != "Admin")
        {
            return new(AdminAccessClassification.NotAdministrator);
        }

        return new(
            AdminAccessClassification.Authorized,
            new AdminContext(member.MemberID));
    }
}
