using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories.IRepositories;

namespace MidProject.Repositories;

public sealed class MemberRepository : IMemberRepository
{
    private readonly AppDbContext _context;

    public MemberRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Member?> GetByIdAsync(int id, bool includeDetails, CancellationToken cancellationToken = default)
    {
        var query = _context.Members.AsQueryable();
        if (includeDetails)
        {
            // 詳情與驗證失敗後的重新顯示都只供畫面使用。使用 no-tracking，
            // 避免把尚未通過驗證的表單值套到同一個被追蹤的 Member，
            // 之後若同 request 內有其他 SaveChanges 時意外寫回資料庫。
            query = query.AsNoTracking()
                .Include(m => m.UserLevel)
                .Include(m => m.AvatarImage);
        }

        return await query.FirstOrDefaultAsync(m => m.MemberID == id, cancellationToken);
    }

    public async Task<List<Report>> GetApprovedReportsAsync(int memberId, CancellationToken cancellationToken = default)
    {
        return await _context.Reports
            .Where(r => r.ReportedMemberID == memberId && r.Status == "Approved")
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserLevel>> GetLevelsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync(cancellationToken);
    }

    public async Task<MemberListPageData> GetIndexPageDataAsync(MemberListFilter filter, CancellationToken cancellationToken = default)
    {
        // 統計卡片、列表都是各自獨立的查詢；用一個唯讀交易（RepeatableRead）把整批查詢包在同一個快照裡，
        // 避免自動懲處排程（MemberEscalationBackgroundService）剛好在這幾個查詢中間執行寫入，
        // 導致統計數字跟下面的列表內容對不上。
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);

        var totalMembers = await _context.Members.CountAsync(m => !m.IsDeleted, cancellationToken);
        var todayRegistered = await _context.Members.CountAsync(m => m.CreatedAt.Date == filter.TodayDate && !m.IsDeleted, cancellationToken);
        var abnormalCount = await _context.Members.CountAsync(m => !m.IsDeleted && m.Status != "Normal", cancellationToken);

        // 基本查詢條件：軟刪除（含累積檢舉自動停權）的會員仍顯示在列表中（反灰、不可點擊），
        // 只是不計入「會員總數」等統計卡片
        var query = _context.Members.Include(m => m.UserLevel).Include(m => m.AvatarImage).AsQueryable();

        if (filter.ShowAbnormal)
        {
            query = query.Where(m => m.Status != "Normal"); // 包含 Warning, Muted, Suspended
        }

        if (filter.TodayOnly)
        {
            query = query.Where(m => m.CreatedAt.Date == filter.TodayDate);
        }

        if (!string.IsNullOrEmpty(filter.Keyword))
        {
            var keyword = filter.Keyword;
            query = query.Where(m => m.UserName.Contains(keyword)
                || (m.NickName != null && m.NickName.Contains(keyword))
                || m.Email.Contains(keyword));
        }

        if (!string.IsNullOrEmpty(filter.StatusFilter))
        {
            query = query.Where(m => m.Status == filter.StatusFilter);
        }
        if (filter.LevelFilter.HasValue)
        {
            query = query.Where(m => m.LevelID == filter.LevelFilter.Value);
        }

        query = filter.SortBy switch
        {
            "lv_asc" => query.OrderBy(m => m.UserLevel.MinExp),
            "lv_desc" => query.OrderByDescending(m => m.UserLevel.MinExp),
            "status_asc" => query.OrderBy(m => m.Status),
            "status_desc" => query.OrderByDescending(m => m.Status),
            _ => query.OrderByDescending(m => m.CreatedAt) // 預設新到舊
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var members = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(cancellationToken);
        var levels = await _context.UserLevels.OrderBy(l => l.MinExp).ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new MemberListPageData(totalMembers, todayRegistered, abnormalCount, members, totalItems, levels);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
}
