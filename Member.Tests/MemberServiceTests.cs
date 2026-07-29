using MidProject.Data;
using MidProject.Models.ViewModels;
using MidProject.Repositories;
using MidProject.Services;
using MidProject.Services.IServices;
using Xunit;
using MemberModel = MidProject.Models.Member;

namespace Member.Tests;

// 覆蓋缺失清單「狀態變更、停權與解除」：驗證規則、稽核欄位（AdminNote/DeletedAt/DeletedBy）
// 都已經搬進 MemberService，這裡直接測 Service，不需要啟動整個 ASP.NET Core 管線。
public class MemberServiceTests
{
    private static (
        MemberService Service,
        AppDbContext Context,
        FakeTaipeiClock Clock,
        RecordingImageLifecycleService ImageLifecycle) CreateService()
    {
        var context = InMemoryDbContextFactory.Create();
        var clock = new FakeTaipeiClock();
        var repository = new MemberRepository(context);
        var imageLifecycle = new RecordingImageLifecycleService();
        var service = new MemberService(repository, clock, imageLifecycle);
        return (service, context, clock, imageLifecycle);
    }

    private static async Task<MemberModel> SeedMemberAsync(AppDbContext context, Action<MemberModel>? configure = null)
    {
        // LevelID 是必填外鍵（UserLevel 關聯設定為 required），InMemory provider 對 Include 一個
        // 找不到對應資料的必要關聯會直接把整筆 Member 濾掉，所以測試一定要先準備好一筆 UserLevel。
        if (!context.UserLevels.Any(l => l.LevelID == 1))
        {
            context.UserLevels.Add(new MidProject.Models.UserLevel { LevelID = 1, LevelName = "新食客", MinExp = 0 });
            await context.SaveChangesAsync();
        }

        var member = new MemberModel
        {
            UserName = "tester",
            NickName = "洛根",
            Email = "tester@example.com",
            PasswordHash = "hash",
            Role = "User",
            Status = "Normal",
            Points = 100,
            LevelID = 1,
            IsActive = true
        };
        configure?.Invoke(member);

        context.Members.Add(member);
        await context.SaveChangesAsync();
        return member;
    }

    private static MemberEditVM BaseModel(MemberModel member) => new()
    {
        MemberID = member.MemberID,
        RowVersion = Convert.ToBase64String(member.RowVersion),
        Status = member.Status,
        AdminNote = member.AdminNote,
        NickName = member.NickName,
        Points = member.Points
    };

    [Fact]
    public async Task SaveMemberEditAsync_StatusChangedWithoutReason_ReturnsValidationFailed()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.Status = "Normal");

        var model = BaseModel(member);
        model.Status = "Warning";
        // 故意不填 StatusChangeReason

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.ValidationFailed, outcome.Kind);
        Assert.True(outcome.ValidationErrors!.ContainsKey(nameof(MemberEditVM.StatusChangeReason)));
    }

    [Fact]
    public async Task SaveMemberEditAsync_StatusChangedWithReason_Succeeds()
    {
        var (service, context, clock, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.Status = "Normal");

        var model = BaseModel(member);
        model.Status = "Warning";
        model.StatusChangeReason = "測試原因";
        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.Success, outcome.Kind);

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.Equal("Warning", updated!.Status);
        Assert.Contains("測試原因", updated.AdminNote);
        Assert.Contains($"{clock.GetNow():yyyy/M/d HH:mm}", updated.AdminNote);
        Assert.Contains("測試管理員", updated.AdminNote);
    }

    [Theory]
    [InlineData("Deleted")]
    [InlineData("Unexpected")]
    public async Task SaveMemberEditAsync_DisallowedStatus_IsRejectedWithoutChangingMember(string status)
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.Status = "Normal");

        var model = BaseModel(member);
        model.Status = status;
        model.StatusChangeReason = "不應套用";

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.ValidationFailed, outcome.Kind);
        Assert.True(outcome.ValidationErrors!.ContainsKey(nameof(MemberEditVM.Status)));

        var unchanged = await context.Members.FindAsync(member.MemberID);
        Assert.Equal("Normal", unchanged!.Status);
        Assert.False(unchanged.IsDeleted);
    }

    // 備註欄在畫面上是 readonly 預覽，真正的來源必須是伺服器端比對出的變更，不能是表單傳入的
    // model.AdminNote —— 否則繞過 UI（例如直接發送 POST）就能偽造任意稽核紀錄。
    [Fact]
    public async Task SaveMemberEditAsync_ForgedAdminNoteInRequest_IsIgnored()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.Status = "Normal");

        var model = BaseModel(member);
        model.Status = "Warning";
        model.StatusChangeReason = "測試原因";
        model.AdminNote = "2000/1/1 00:00 已將狀態從「停權」變更為「正常」，原因：假造紀錄（操作人員：假冒的人）";

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.Success, outcome.Kind);

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.DoesNotContain("假造紀錄", updated!.AdminNote);
        Assert.DoesNotContain("假冒的人", updated.AdminNote);
        Assert.DoesNotContain("2000/1/1", updated.AdminNote);
    }

    [Fact]
    public async Task SaveMemberEditAsync_SuspendMember_SetsIsDeletedAndDeletedByToCurrentAdmin()
    {
        var (service, context, clock, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.Status = "Muted");

        var model = BaseModel(member);
        model.Status = "Suspended";
        model.StatusChangeReason = "累積違規";

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(42, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.Success, outcome.Kind);

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.True(updated!.IsDeleted);
        Assert.Equal(42, updated.DeletedBy);
        Assert.Equal(clock.GetNow(), updated.DeletedAt);
    }

    [Fact]
    public async Task SaveMemberEditAsync_UnsuspendMember_ClearsIsDeletedAndDeletedBy()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m =>
        {
            m.Status = "Suspended";
            m.IsDeleted = true;
            m.DeletedBy = 42;
            m.DeletedAt = DateTime.UtcNow;
        });

        var model = BaseModel(member);
        model.Status = "Normal";
        model.StatusChangeReason = "解除停權";

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.Success, outcome.Kind);

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.False(updated!.IsDeleted);
        Assert.Null(updated.DeletedBy);
        Assert.Null(updated.DeletedAt);
        Assert.Null(updated.PenaltyEndAt);
    }

    [Fact]
    public async Task SaveMemberEditAsync_NicknameChangedWithoutReason_ReturnsValidationFailed()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.NickName = "舊名稱");

        var model = BaseModel(member);
        model.NickName = "新名稱";
        // 故意不填 NicknameChangeReason

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.ValidationFailed, outcome.Kind);
        Assert.True(outcome.ValidationErrors!.ContainsKey(nameof(MemberEditVM.NicknameChangeReason)));
    }

    [Fact]
    public async Task SaveMemberEditAsync_AvatarRemovalWithoutReason_ReturnsValidationFailed()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.AvatarImageID = 999);

        var model = BaseModel(member);
        model.RemoveAvatarRequested = true;
        // 故意不填 AvatarRemovalReason

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.ValidationFailed, outcome.Kind);
        Assert.True(outcome.ValidationErrors!.ContainsKey(nameof(MemberEditVM.AvatarRemovalReason)));
    }

    [Fact]
    public async Task SaveMemberEditAsync_AvatarRemoval_CleansUpPreviousImage()
    {
        var (service, context, _, imageLifecycle) = CreateService();
        var member = await SeedMemberAsync(context, m => m.AvatarImageID = 999);

        var model = BaseModel(member);
        model.RemoveAvatarRequested = true;
        model.AvatarRemovalReason = "使用者要求移除";

        var outcome = await service.SaveMemberEditAsync(
            member.MemberID,
            model,
            new MemberEditOperator(42, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.Success, outcome.Kind);
        Assert.Null((await context.Members.FindAsync(member.MemberID))!.AvatarImageID);
        Assert.Equal([999], imageLifecycle.CleanedImageIds);
        Assert.Equal(42, imageLifecycle.DeletedByMemberId);
    }

    [Fact]
    public async Task SaveMemberEditAsync_PointsChangedWithoutReason_ReturnsValidationFailed()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.Points = 100);

        var model = BaseModel(member);
        model.Points = 200;
        // 故意不填 PointsChangeReason

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.ValidationFailed, outcome.Kind);
        Assert.True(outcome.ValidationErrors!.ContainsKey(nameof(MemberEditVM.PointsChangeReason)));
    }

    [Fact]
    public async Task SaveMemberEditAsync_NegativePoints_IsRejectedWithoutWriting()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.Points = 100);

        var model = BaseModel(member);
        model.Points = -1;
        model.PointsChangeReason = "惡意繞過前端驗證";

        var outcome = await service.SaveMemberEditAsync(
            member.MemberID,
            model,
            new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.ValidationFailed, outcome.Kind);
        Assert.True(outcome.ValidationErrors!.ContainsKey(nameof(MemberEditVM.Points)));
        Assert.Equal(100, (await context.Members.FindAsync(member.MemberID))!.Points);
    }

    [Fact]
    public async Task SaveMemberEditAsync_PointsChanged_WritesAdminAdjustTransaction()
    {
        var (service, context, clock, _) = CreateService();
        var member = await SeedMemberAsync(context, m => m.Points = 100);

        var model = BaseModel(member);
        model.Points = 175;
        model.PointsChangeReason = "客服補償";

        var outcome = await service.SaveMemberEditAsync(
            member.MemberID,
            model,
            new MemberEditOperator(42, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.Success, outcome.Kind);
        Assert.Equal(175, (await context.Members.FindAsync(member.MemberID))!.Points);

        var transaction = Assert.Single(context.PointsTransactions);
        Assert.Equal(member.MemberID, transaction.MemberID);
        Assert.Equal(75, transaction.Amount);
        Assert.Equal(175, transaction.BalanceAfter);
        Assert.Equal("AdminAdjust", transaction.Type);
        Assert.Equal("客服補償", transaction.Note);
        Assert.Equal(42, transaction.CreatedBy);
        Assert.Equal(clock.Now, transaction.CreatedAt);
    }

    [Fact]
    public async Task SaveMemberEditAsync_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m =>
        {
            m.Points = 100;
            m.RowVersion = new byte[] { 1 };
        });

        var model = BaseModel(member);
        model.RowVersion = Convert.ToBase64String(new byte[] { 0 });
        model.Points = 200;
        model.PointsChangeReason = "測試並行更新";

        var outcome = await service.SaveMemberEditAsync(
            member.MemberID,
            model,
            new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.ConcurrencyConflict, outcome.Kind);
        Assert.Equal(100, (await context.Members.FindAsync(member.MemberID))!.Points);
    }

    [Fact]
    public async Task SaveMemberEditAsync_UnlockWithoutReason_SucceedsBecauseReasonIsOptional()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m =>
        {
            m.IsLocked = true;
            m.FailedLoginCount = 3;
        });

        var model = BaseModel(member);
        model.UnlockAccountRequested = true;
        // 解除鎖定原因為選填，不填也應該成功

        var outcome = await service.SaveMemberEditAsync(member.MemberID, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.Success, outcome.Kind);

        var updated = await context.Members.FindAsync(member.MemberID);
        Assert.False(updated!.IsLocked);
        Assert.Equal(0, updated.FailedLoginCount);
        Assert.Null(updated.LoginLockoutEndAt);
    }

    [Fact]
    public async Task SaveMemberEditAsync_MemberNotFound_ReturnsNotFound()
    {
        var (service, _, _, _) = CreateService();

        var model = new MemberEditVM { MemberID = 999, Status = "Normal" };
        var outcome = await service.SaveMemberEditAsync(999, model, new MemberEditOperator(1, "測試管理員"));

        Assert.Equal(MemberEditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task GetEditViewDataAsync_ExistingMember_ReturnsOriginalValuesFromDatabase()
    {
        var (service, context, _, _) = CreateService();
        var member = await SeedMemberAsync(context, m =>
        {
            m.Status = "Muted";
            m.NickName = "洛根";
            m.Points = 250;
        });

        var data = await service.GetEditViewDataAsync(member.MemberID);

        Assert.NotNull(data);
        Assert.Equal("Muted", data!.OriginalStatus);
        Assert.Equal("洛根", data.OriginalNickName);
        Assert.Equal(250, data.OriginalPoints);
    }

    [Fact]
    public async Task GetEditViewDataAsync_MissingMember_ReturnsNull()
    {
        var (service, _, _, _) = CreateService();

        var data = await service.GetEditViewDataAsync(999);

        Assert.Null(data);
    }
}
