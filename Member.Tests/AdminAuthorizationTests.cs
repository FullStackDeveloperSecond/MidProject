using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Member.Tests;

// 覆蓋缺失清單「Admin 權限」：後台控制器都必須要求 Admin 角色才能進入，
// 用反射檢查 [Authorize(Roles = "Admin")] 是否還掛在正確的控制器上，
// 避免日後有人不小心把這個屬性拿掉卻沒有測試會失敗提醒。
public class AdminAuthorizationTests
{
    [Theory]
    [InlineData("AdminMembersController")]
    [InlineData("ReportsController")]
    [InlineData("ReviewsController")]
    [InlineData("RestaurantsController")]
    [InlineData("TagsController")]
    public void Controller_RequiresAdminRole(string controllerTypeName)
    {
        var controllerType = FindControllerType(controllerTypeName);
        Assert.NotNull(controllerType);

        var authorizeAttribute = controllerType!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal("Admin", authorizeAttribute!.Roles);
    }

    private static Type? FindControllerType(string typeName)
    {
        var assembly = Assembly.Load("MidProject");
        return assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
    }
}
