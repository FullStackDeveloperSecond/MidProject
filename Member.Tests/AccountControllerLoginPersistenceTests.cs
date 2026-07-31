using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MidProject.Data;
using MidProject.Models;
using MidProject.Services;

namespace Member.Tests;

public sealed class AccountControllerLoginPersistenceTests
{
    [Fact]
    public async Task Login_NonAdminWrongPassword_PersistsFailureBeforeRoleRejection()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        const string email = "member@example.com";
        var clock = new FakeTaipeiClock
        {
            Now = new DateTime(2026, 7, 30, 13, 0, 0)
        };

        await using var context = new AppDbContext(options);
        context.Members.Add(new MidProject.Models.Member
        {
            UserName = "member",
            NickName = "member",
            Email = email,
            PasswordHash = PasswordHashService.HashPassword("Correct123!"),
            Role = "User",
            Status = "Normal",
            IsActive = true,
            LevelID = 1,
            CreatedAt = clock.Now,
            UpdatedAt = clock.Now
        });
        await context.SaveChangesAsync();
        var controller = new AccountController(context, clock);

        var result = await controller.Login(email, "Wrong123!");

        Assert.IsType<ViewResult>(result);
        var stored = await context.Members
            .AsNoTracking()
            .SingleAsync(member => member.Email == email);
        Assert.Equal(1, stored.FailedLoginCount);
        Assert.False(stored.IsLocked);
    }

    [Fact]
    public async Task Login_ThreeWrongPasswords_PersistsCountAndLocksMember()
    {
        var root = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), root)
            .Options;
        const string email = "admin@example.com";
        var clock = new FakeTaipeiClock
        {
            Now = new DateTime(2026, 7, 30, 13, 0, 0)
        };

        await using (var arrangeContext = new AppDbContext(options))
        {
            arrangeContext.Members.Add(new MidProject.Models.Member
            {
                UserName = "Admin",
                NickName = "Admin",
                Email = email,
                PasswordHash = PasswordHashService.HashPassword("Correct123!"),
                Role = "Admin",
                Status = "Normal",
                IsActive = true,
                LevelID = 1,
                CreatedAt = clock.Now,
                UpdatedAt = clock.Now
            });
            await arrangeContext.SaveChangesAsync();
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using var attemptContext = new AppDbContext(options);
            var controller = new AccountController(attemptContext, clock);

            var result = await controller.Login(email, "Wrong123!");

            Assert.IsType<ViewResult>(result);
            var stored = await attemptContext.Members
                .AsNoTracking()
                .SingleAsync(member => member.Email == email);
            Assert.Equal(attempt, stored.FailedLoginCount);
            Assert.Equal(attempt == 3, stored.IsLocked);

            if (attempt == 3)
            {
                var expectedLockoutEnd =
                    clock.Now.Add(MemberLoginPolicy.LoginLockoutDuration);
                Assert.Equal(expectedLockoutEnd, stored.LoginLockoutEndAt);
                Assert.Equal(
                    MemberLoginPolicy.GetLockoutMessage(expectedLockoutEnd),
                    controller.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
            }
        }
    }
}
