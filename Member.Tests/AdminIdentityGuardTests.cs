using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MidProject.Controllers;
using MidProject.Models.ViewModels.Restaurants;
using MidProject.Models.ViewModels.Tags;
using MidProject.Services.IServices;

namespace Member.Tests;

public class AdminIdentityGuardTests
{
    [Fact]
    public async Task DisableRestaurant_MissingMemberIdClaim_ReturnsForbidWithoutCallingService()
    {
        var service = new RecordingRestaurantService();
        var controller = WithAdminWithoutMemberId(new RestaurantsController(service));

        var result = await controller.Disable(42, "測試");

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.DisableCallCount);
    }

    [Fact]
    public async Task CreateTag_InvalidMemberIdClaim_ReturnsForbidWithoutCallingService()
    {
        var service = new RecordingTagService();
        var controller = WithAdminWithoutMemberId(new TagsController(service), "not-an-id");

        var result = await controller.Create("新標籤");

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.CreateCallCount);
    }

    private static T WithAdminWithoutMemberId<T>(T controller, string? memberId = null)
        where T : Controller
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "Admin") };
        if (memberId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, memberId));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
        return controller;
    }

    private sealed class RecordingTagService : ITagService
    {
        public int CreateCallCount { get; private set; }

        public Task<TagsIndexViewModel> GetIndexAsync() => throw new NotSupportedException();

        public Task<(bool Success, string? Error)> CreateAsync(string name, int adminId)
        {
            CreateCallCount++;
            return Task.FromResult<(bool, string?)>((true, null));
        }

        public Task<bool> ToggleAsync(int id, int adminId) => throw new NotSupportedException();

        public Task<bool> ReorderAsync(IReadOnlyList<int> orderedIds) => throw new NotSupportedException();
    }

    private sealed class RecordingRestaurantService : IRestaurantService
    {
        public int DisableCallCount { get; private set; }

        public Task<RestaurantIndexViewModel> GetIndexAsync(RestaurantFilterQuery filter) =>
            throw new NotSupportedException();

        public Task<RestaurantDeletedIndexViewModel> GetDeletedIndexAsync(RestaurantDeletedFilterQuery filter) =>
            throw new NotSupportedException();

        public Task<RestaurantDetailViewModel?> GetDetailAsync(int id) => throw new NotSupportedException();

        public Task<RestaurantFormViewModel> GetCreateFormAsync() => throw new NotSupportedException();

        public Task<RestaurantFormViewModel?> GetEditFormAsync(int id) => throw new NotSupportedException();

        public Task<(bool Success, int? NewId)> CreateAsync(RestaurantFormViewModel form, int adminId) =>
            throw new NotSupportedException();

        public Task<bool> EditAsync(int id, RestaurantFormViewModel form, int adminId) =>
            throw new NotSupportedException();

        public Task<RestaurantFormViewModel> RehydrateFormAsync(RestaurantFormViewModel form) =>
            throw new NotSupportedException();

        public Task<bool> DisableAsync(int id, string reason, int? byMemberId = null)
        {
            DisableCallCount++;
            return Task.FromResult(true);
        }

        public Task<bool> RestoreAsync(int id) => throw new NotSupportedException();
    }
}
