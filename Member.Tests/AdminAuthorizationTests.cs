using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using MidProject.Services;
using Xunit;

namespace Member.Tests;

// 覆蓋缺失清單「Admin 權限」：後台控制器都必須使用共用的 AdminAuthorizationFilter，
// 由同一套 evaluator 在每個受保護請求重新驗證管理員狀態。
public class AdminAuthorizationTests
{
    [Theory]
    [InlineData("AdminMembersController")]
    [InlineData("ReportsController")]
    [InlineData("ReviewsController")]
    [InlineData("RestaurantsController")]
    [InlineData("TagsController")]
    public void Controller_UsesSharedAdminAuthorizationFilter(string controllerTypeName)
    {
        var controllerType = FindControllerType(controllerTypeName);
        Assert.NotNull(controllerType);

        var serviceFilter = controllerType!.GetCustomAttributes<ServiceFilterAttribute>()
            .SingleOrDefault(attribute =>
                attribute.ServiceType == typeof(AdminAuthorizationFilter));

        Assert.NotNull(serviceFilter);
    }

    private static Type? FindControllerType(string typeName)
    {
        var assembly = Assembly.Load("MidProject");
        return assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
    }
}
