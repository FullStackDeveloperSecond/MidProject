using MidProject.Models;
using MidProject.Models.ViewModels;
using MidProject.Repositories.IRepositories;

namespace MidProject.Services.IServices;

// 會員列表頁／編輯頁的業務邏輯層。AdminMembersController 只會呼叫這裡，
// 不會直接碰 IMemberRepository 或 AppDbContext（Controller → Service → Repository → DbContext）。
public interface IMemberService
{
    Task<MemberListPageData> GetIndexViewDataAsync(MemberIndexQuery query, CancellationToken cancellationToken = default);
    Task<MemberEditViewData?> GetEditViewDataAsync(int id, CancellationToken cancellationToken = default);
    Task<MemberEditOutcome> SaveMemberEditAsync(int id, MemberEditVM model, MemberEditOperator memberEditOperator, CancellationToken cancellationToken = default);
}

// 執行變更的管理員身分，一律來自 Controller 端已驗證過的登入 Cookie Claims
// （[Authorize(Roles="Admin")] 保證進得來的一定是登入中的 Admin），不會是表單欄位、不可能被使用者偽造。
// Service 組稽核紀錄文字、寫入 DeletedBy 時都只採用這裡的資料。
public sealed record MemberEditOperator(int MemberId, string DisplayName);

public sealed record MemberIndexQuery(
    string? Keyword,
    string? StatusFilter,
    int? LevelFilter,
    string? SortBy,
    bool ShowAbnormal,
    bool TodayOnly,
    int Page);

// 會員編輯頁需要顯示的所有資料，統一包成一個物件，取代原本散落各處的 ViewBag 賦值來源。
// Controller 仍然會把這些欄位攤平回 ViewBag（維持 Edit.cshtml 完全不用改），
// 但資料本身是從 Service 一次拿齊，Controller 不用自己組。
public sealed class MemberEditViewData
{
    public required Member Member { get; init; }
    public required List<Report> ApprovedReports { get; init; }
    public required List<UserLevel> Levels { get; init; }
    public required string OriginalStatus { get; init; }
    public string? OriginalNickName { get; init; }
    public required int OriginalPoints { get; init; }

    // 以下欄位只有在「保存變更」驗證失敗、需要重新顯示表單時才會有值，
    // 用來把使用者剛才在畫面上填寫/預覽的內容原樣帶回去，避免送出失敗後資料消失。
    public string? StatusChangeReason { get; init; }
    public string? NicknameChangeReason { get; init; }
    public bool RemoveAvatarRequested { get; init; }
    public string? AvatarRemovalReason { get; init; }
    public string? PointsChangeReason { get; init; }
    public bool UnlockAccountRequested { get; init; }
    public string? UnlockReason { get; init; }
}

public enum MemberEditOutcomeKind
{
    Success,
    ValidationFailed,
    NotFound
}

public sealed class MemberEditOutcome
{
    public required MemberEditOutcomeKind Kind { get; init; }
    public Dictionary<string, string>? ValidationErrors { get; init; }
    public MemberEditViewData? RedisplayData { get; init; }

    public static MemberEditOutcome NotFound() => new() { Kind = MemberEditOutcomeKind.NotFound };

    public static MemberEditOutcome Success() => new() { Kind = MemberEditOutcomeKind.Success };

    public static MemberEditOutcome ValidationFailed(Dictionary<string, string> errors, MemberEditViewData redisplayData) =>
        new() { Kind = MemberEditOutcomeKind.ValidationFailed, ValidationErrors = errors, RedisplayData = redisplayData };
}
