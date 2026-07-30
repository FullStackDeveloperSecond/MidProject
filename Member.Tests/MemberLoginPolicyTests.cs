using MidProject.Models;
using MidProject.Services;
using Xunit;

namespace Member.Tests;

// 覆蓋缺失清單「登入資格」：這個網站只給 Admin 登入；鎖定／停用／停權一律拒絕登入；
// 連續密碼錯誤 3 次鎖定帳號。
public class MemberLoginPolicyTests
{
    // 這個網站只開放 Admin 登入，所以「合格」的基準帳號本身就要是 Admin 角色
    private static MidProject.Models.Member CreateEligibleMember() => new()
    {
        MemberID = 1,
        UserName = "tester",
        Email = "tester@example.com",
        PasswordHash = "hash",
        Role = "Admin",
        Status = "Normal",
        IsActive = true,
        IsLocked = false,
        IsDeleted = false
    };

    [Fact]
    public void CheckEligibility_NonAdminMember_ReturnsRoleRestrictionMessage()
    {
        var member = CreateEligibleMember();
        member.Role = "User";

        var result = MemberLoginPolicy.CheckEligibility(member);

        Assert.NotNull(result);
        Assert.Contains("管理員", result);
    }

    [Fact]
    public void CheckEligibility_LockedMember_ReturnsLockedMessage()
    {
        var member = CreateEligibleMember();
        member.IsLocked = true;

        var result = MemberLoginPolicy.CheckEligibility(member);

        Assert.NotNull(result);
        Assert.Contains("鎖定", result);
    }

    [Fact]
    public void CheckEligibility_InactiveMember_ReturnsInactiveMessage()
    {
        var member = CreateEligibleMember();
        member.IsActive = false;

        var result = MemberLoginPolicy.CheckEligibility(member);

        Assert.NotNull(result);
        Assert.Contains("停用", result);
    }

    [Fact]
    public void CheckEligibility_SuspendedMember_ReturnsSuspendedMessage()
    {
        var member = CreateEligibleMember();
        member.Status = "Suspended";
        member.IsDeleted = true;

        var result = MemberLoginPolicy.CheckEligibility(member);

        Assert.NotNull(result);
        Assert.Contains("停權", result);
    }

    [Fact]
    public void CheckEligibility_DeletedMember_ReturnsDeletedMessage()
    {
        var member = CreateEligibleMember();
        member.Status = "Deleted";
        member.IsDeleted = true;

        var result = MemberLoginPolicy.CheckEligibility(member);

        Assert.NotNull(result);
        Assert.Contains("刪除", result);
    }

    [Theory]
    [InlineData("Normal")]
    [InlineData("Warning")]
    [InlineData("Muted")]
    public void CheckEligibility_ActiveUnlockedNonSuspendedMember_ReturnsNull(string status)
    {
        var member = CreateEligibleMember();
        member.Status = status;

        var result = MemberLoginPolicy.CheckEligibility(member);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 2, false)]
    [InlineData(2, 3, true)]
    [InlineData(5, 6, true)]
    public void RecordFailedAttempt_IncrementsAndLocksAtThreshold(int currentCount, int expectedNewCount, bool expectedLock)
    {
        var (newCount, shouldLock) = MemberLoginPolicy.RecordFailedAttempt(currentCount);

        Assert.Equal(expectedNewCount, newCount);
        Assert.Equal(expectedLock, shouldLock);
    }

    [Fact]
    public void HasExpiredLoginLockout_WhenTemporaryLockoutExpired_ReturnsTrue()
    {
        var now = new DateTime(2026, 7, 29, 10, 0, 0);
        var member = CreateEligibleMember();
        member.IsLocked = true;
        member.LoginLockoutEndAt = now.AddSeconds(-1);

        Assert.True(MemberLoginPolicy.HasExpiredLoginLockout(member, now));
    }

    [Fact]
    public void HasExpiredLoginLockout_WhenManualLockHasNoEnd_ReturnsFalse()
    {
        var member = CreateEligibleMember();
        member.IsLocked = true;
        member.LoginLockoutEndAt = null;

        Assert.False(MemberLoginPolicy.HasExpiredLoginLockout(member, DateTime.Now));
    }

    [Fact]
    public void GetLockoutMessage_WhenTemporaryLockout_ReturnsThresholdAndEndTime()
    {
        var lockoutEnd = new DateTime(2026, 7, 30, 14, 25, 0);

        var result = MemberLoginPolicy.GetLockoutMessage(lockoutEnd);

        Assert.Contains("3 次", result);
        Assert.Contains("帳號已暫時鎖定", result);
        Assert.Contains("2026/07/30 14:25", result);
    }

    [Fact]
    public void GetLockoutMessage_WhenManualLock_ReturnsContactAdministratorMessage()
    {
        var result = MemberLoginPolicy.GetLockoutMessage(null);

        Assert.Contains("帳號已鎖定", result);
        Assert.Contains("聯繫管理員", result);
    }
}
