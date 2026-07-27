using MidProject.Models.ViewModels;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

// 會員列表頁／編輯頁的業務邏輯：自動懲處以外的手動調整（改名稱／移除照片／調整點數／解除鎖定／變更狀態）
// 的驗證規則、稽核欄位（AdminNote／DeletedAt／DeletedBy）都集中在這裡，Controller 不再直接碰 AppDbContext。
public sealed class MemberService : IMemberService
{
    private readonly IMemberRepository _repository;
    private readonly ITaipeiClock _clock;

    public MemberService(IMemberRepository repository, ITaipeiClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<MemberListPageData> GetIndexViewDataAsync(MemberIndexQuery query, CancellationToken cancellationToken = default)
    {
        var filter = new MemberListFilter(
            query.Keyword,
            query.StatusFilter,
            query.LevelFilter,
            query.SortBy,
            query.ShowAbnormal,
            query.TodayOnly,
            query.Page,
            _clock.GetNow().Date);

        return await _repository.GetIndexPageDataAsync(filter, cancellationToken);
    }

    public async Task<MemberEditViewData?> GetEditViewDataAsync(int id, CancellationToken cancellationToken = default)
    {
        // 停權會員仍可開啟編輯頁（管理員可解除停權），故不排除 IsDeleted
        var member = await _repository.GetByIdAsync(id, includeDetails: true, cancellationToken);
        if (member == null) return null;

        var approvedReports = await _repository.GetApprovedReportsAsync(id, cancellationToken);
        var levels = await _repository.GetLevelsAsync(cancellationToken);

        return new MemberEditViewData
        {
            Member = member,
            ApprovedReports = approvedReports,
            Levels = levels,
            OriginalStatus = member.Status,
            OriginalNickName = member.NickName,
            OriginalPoints = member.Points
        };
    }

    private static readonly Dictionary<string, string> StatusLabels = new()
    {
        ["Normal"] = "正常",
        ["Warning"] = "警告",
        ["Muted"] = "禁言",
        ["Suspended"] = "停權"
    };

    private static string StatusLabel(string status) => StatusLabels.TryGetValue(status, out var label) ? label : status;

    public async Task<MemberEditOutcome> SaveMemberEditAsync(int id, MemberEditVM model, MemberEditOperator memberEditOperator, CancellationToken cancellationToken = default)
    {
        var memberInDb = await _repository.GetByIdAsync(id, includeDetails: false, cancellationToken);
        if (memberInDb == null)
        {
            return MemberEditOutcome.NotFound();
        }

        // 每個「手動調整」子動作都先在前端彈窗確認、即時把變更帶入對應的隱藏欄位並在 AdminNote 文字框
        // 顯示預覽（僅供畫面顯示，送出後端一律忽略），尚未真的送出到資料庫；要按下這個表單的「保存變更」
        // 才會一次性套用所有已預覽的變更並真正保存（跟「解除停權」原本的流程一致）。
        var statusChanged = memberInDb.Status != model.Status;
        var nicknameChanged = model.NickName != null && memberInDb.NickName != model.NickName;
        var avatarRemoval = model.RemoveAvatarRequested && memberInDb.AvatarImageID != null;
        var pointsChanged = memberInDb.Points != model.Points;
        var unlockRequested = model.UnlockAccountRequested && memberInDb.IsLocked;

        var errors = new Dictionary<string, string>();
        if (statusChanged && string.IsNullOrWhiteSpace(model.StatusChangeReason))
        {
            errors[nameof(MemberEditVM.StatusChangeReason)] = "變更會員狀態時，請填寫變更原因。";
        }
        if (nicknameChanged && string.IsNullOrWhiteSpace(model.NicknameChangeReason))
        {
            errors[nameof(MemberEditVM.NicknameChangeReason)] = "變更名稱時，請填寫變更原因。";
        }
        if (avatarRemoval && string.IsNullOrWhiteSpace(model.AvatarRemovalReason))
        {
            errors[nameof(MemberEditVM.AvatarRemovalReason)] = "移除照片時，請填寫原因。";
        }
        if (pointsChanged && string.IsNullOrWhiteSpace(model.PointsChangeReason))
        {
            errors[nameof(MemberEditVM.PointsChangeReason)] = "調整點數時，請填寫原因。";
        }
        // 解除鎖定原因為選填，不需驗證

        if (errors.Count > 0)
        {
            var redisplayData = await BuildRedisplayDataAsync(
                id, model, memberInDb.Status, memberInDb.NickName, memberInDb.Points, cancellationToken);
            return MemberEditOutcome.ValidationFailed(errors, redisplayData);
        }

        var now = _clock.GetNow();
        var timestamp = now.ToString("yyyy/M/d HH:mm");

        // 5.3 允許編輯欄位（帳號 UserName 不可變更）
        // AdminNote 一律由後端依「資料庫比對出的實際變更」與已驗證過的原因欄位重新組字串，絕不採用
        // 表單送來的 model.AdminNote —— 該欄位在畫面上雖是 readonly 預覽，但 <textarea> 的內容仍會
        // 隨表單一起送出，繞過 UI（例如直接發送 POST）就能把任意文字寫進稽核紀錄。時間戳記、比對用的
        // 新舊值都取自伺服器端（_clock／memberInDb），操作人員身分也只採用 Controller 端已驗證過的
        // memberEditOperator，全部不接受表單欄位覆寫。
        var noteLines = new List<string>();
        if (statusChanged)
        {
            noteLines.Add($"{timestamp} 已將狀態從「{StatusLabel(memberInDb.Status)}」變更為「{StatusLabel(model.Status)}」，原因：{model.StatusChangeReason}（操作人員：{memberEditOperator.DisplayName}）");
        }
        if (nicknameChanged)
        {
            noteLines.Add($"{timestamp} 已將名稱從「{memberInDb.NickName}」變更為「{model.NickName}」，原因：{model.NicknameChangeReason}（操作人員：{memberEditOperator.DisplayName}）");
        }
        if (avatarRemoval)
        {
            noteLines.Add($"{timestamp} 因{model.AvatarRemovalReason}，已移除照片（操作人員：{memberEditOperator.DisplayName}）");
        }
        if (pointsChanged)
        {
            noteLines.Add($"{timestamp} 已將點數從 {memberInDb.Points} 調整為 {model.Points}，原因：{model.PointsChangeReason}（操作人員：{memberEditOperator.DisplayName}）");
        }
        if (unlockRequested)
        {
            noteLines.Add(string.IsNullOrWhiteSpace(model.UnlockReason)
                ? $"{timestamp} 已解除帳號鎖定（操作人員：{memberEditOperator.DisplayName}）"
                : $"{timestamp} 已解除帳號鎖定，原因：{model.UnlockReason}（操作人員：{memberEditOperator.DisplayName}）");
        }
        if (noteLines.Count > 0)
        {
            var combinedNote = string.Join('\n', noteLines);
            memberInDb.AdminNote = string.IsNullOrWhiteSpace(memberInDb.AdminNote)
                ? combinedNote
                : combinedNote + '\n' + memberInDb.AdminNote;
        }
        memberInDb.Status = model.Status;

        if (nicknameChanged)
        {
            memberInDb.NickName = model.NickName;
        }
        if (avatarRemoval)
        {
            memberInDb.AvatarImageID = null;
        }
        if (pointsChanged)
        {
            memberInDb.Points = model.Points;
        }
        if (unlockRequested)
        {
            memberInDb.IsLocked = false;
            memberInDb.FailedLoginCount = 0;
        }

        // 5.5 處分期限現在由自動懲處排程（MemberEscalationBackgroundService）依受理檢舉次數計算，
        // 手動編輯僅在「正常／停權」時清空期限，其餘狀態維持既有的處分期限不變
        if (model.Status is "Normal" or "Suspended")
        {
            memberInDb.PenaltyEndAt = null;
        }

        // 5.6 & 11. Status = Suspended 的連動規則（軟刪除：僅標記，不移除資料列；
        // 管理員仍可將狀態改回正常/警告以解除停權，此時清除軟刪除標記）
        if (model.Status == "Suspended")
        {
            memberInDb.IsDeleted = true;
            memberInDb.DeletedAt = now;
            memberInDb.DeletedBy = memberEditOperator.MemberId;
        }
        else
        {
            memberInDb.IsDeleted = false;
            memberInDb.DeletedAt = null;
            memberInDb.DeletedBy = null;
        }

        memberInDb.UpdatedAt = now;

        await _repository.SaveChangesAsync(cancellationToken);
        return MemberEditOutcome.Success();
    }

    // 驗證失敗時：重新查詢資料庫中完整且正確的會員資料（含等級、經驗值、點數、頭像等），
    // 只疊加使用者這次在表單中實際編輯的欄位，避免顯示未繫結欄位的預設值
    private async Task<MemberEditViewData> BuildRedisplayDataAsync(
        int id, MemberEditVM model, string originalStatus, string? originalNickName, int originalPoints, CancellationToken cancellationToken)
    {
        var displayMember = await _repository.GetByIdAsync(id, includeDetails: true, cancellationToken);
        if (displayMember != null)
        {
            displayMember.Status = model.Status;
            displayMember.AdminNote = model.AdminNote;
            if (model.NickName != null)
            {
                displayMember.NickName = model.NickName;
            }
            if (model.RemoveAvatarRequested)
            {
                displayMember.AvatarImageID = null;
                displayMember.AvatarImage = null;
            }
            displayMember.Points = model.Points;
        }

        var approvedReports = await _repository.GetApprovedReportsAsync(id, cancellationToken);
        var levels = await _repository.GetLevelsAsync(cancellationToken);

        return new MemberEditViewData
        {
            Member = displayMember!,
            ApprovedReports = approvedReports,
            Levels = levels,
            OriginalStatus = originalStatus,
            OriginalNickName = originalNickName,
            OriginalPoints = originalPoints,
            StatusChangeReason = model.StatusChangeReason,
            NicknameChangeReason = model.NicknameChangeReason,
            RemoveAvatarRequested = model.RemoveAvatarRequested,
            AvatarRemovalReason = model.AvatarRemovalReason,
            PointsChangeReason = model.PointsChangeReason,
            UnlockAccountRequested = model.UnlockAccountRequested,
            UnlockReason = model.UnlockReason
        };
    }
}
