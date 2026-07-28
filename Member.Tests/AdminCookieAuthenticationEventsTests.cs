using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MidProject.Services;
using Xunit;

namespace Member.Tests;

public sealed class AdminCookieAuthenticationEventsTests
{
    [Fact]
    public async Task ValidatePrincipal_EligibleAdmin_KeepsPrincipal()
    {
        await using var context = InMemoryDbContextFactory.Create();
        var member = TestAdmin();
        context.Members.Add(member);
        await context.SaveChangesAsync();

        var validationContext = CreateValidationContext(member.MemberID);
        var events = new AdminCookieAuthenticationEvents(context);

        await events.ValidatePrincipal(validationContext);

        Assert.NotNull(validationContext.Principal);
    }

    [Fact]
    public async Task ValidatePrincipal_SuspendedAdmin_RejectsExistingCookie()
    {
        await using var context = InMemoryDbContextFactory.Create();
        var member = TestAdmin();
        member.Status = "Suspended";
        member.IsDeleted = true;
        context.Members.Add(member);
        await context.SaveChangesAsync();

        var validationContext = CreateValidationContext(member.MemberID);
        var events = new AdminCookieAuthenticationEvents(context);

        await events.ValidatePrincipal(validationContext);

        Assert.Null(validationContext.Principal);
    }

    private static MidProject.Models.Member TestAdmin() => new()
    {
        MemberID = 1,
        UserName = "admin",
        NickName = "管理員",
        Email = "admin@example.com",
        PasswordHash = "hash",
        Role = "Admin",
        Status = "Normal",
        IsActive = true,
        LevelID = 1
    };

    private static CookieValidatePrincipalContext CreateValidationContext(int memberId)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            .Services
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, memberId.ToString()),
                new Claim(ClaimTypes.Role, "Admin")
            },
            CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var scheme = new AuthenticationScheme(
            CookieAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(
            principal,
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new CookieValidatePrincipalContext(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            ticket);
    }
}
