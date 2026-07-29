using MidProject.Models;

namespace MidProject.Repositories.IRepositories;

// 會員模組的資料存取層。AdminMembersController 不會直接碰 AppDbContext，
// 一律經過 IMemberService 再到這裡（Controller → Service → Repository → DbContext）。
public interface IMemberRepository
{
    Task<Member?> GetByIdAsync(int id, bool includeDetails, CancellationToken cancellationToken = default);
    Task<List<Report>> GetApprovedReportsAsync(int memberId, CancellationToken cancellationToken = default);
    Task<List<UserLevel>> GetLevelsAsync(CancellationToken cancellationToken = default);
    Task<MemberListPageData> GetIndexPageDataAsync(MemberListFilter filter, CancellationToken cancellationToken = default);
    void AddPointsTransaction(PointsTransaction transaction);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed record MemberListFilter(
    string? Keyword,
    string? StatusFilter,
    int? LevelFilter,
    string? SortBy,
    bool ShowAbnormal,
    bool TodayOnly,
    int Page,
    DateTime TodayDate,
    int PageSize = 10);

public sealed record MemberListPageData(
    int TotalMembers,
    int TodayRegistered,
    int AbnormalCount,
    List<Member> Members,
    int TotalItems,
    List<UserLevel> Levels);
