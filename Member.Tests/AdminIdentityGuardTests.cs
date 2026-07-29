using Microsoft.AspNetCore.Mvc;
using MidProject.Controllers;
using MidProject.Models.ViewModels.Restaurants;
using MidProject.Models.ViewModels.Tags;
using MidProject.Services.IServices;

namespace Member.Tests;

public class AdminIdentityGuardTests
{
    [Fact]
    public async Task DisableRestaurant_UsesValidatedCurrentAdminId()
    {
        var service = new RecordingRestaurantService();
        var controller = new RestaurantsController(service, new FixedCurrentAdminAccessor(123));

        var result = await controller.Disable(42, "測試");

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(1, service.DisableCallCount);
        Assert.Equal(123, service.LastAdminId);
    }

    [Fact]
    public async Task ToggleTag_UsesValidatedCurrentAdminId()
    {
        var service = new RecordingTagService();
        var controller = new TagsController(service, new FixedCurrentAdminAccessor(456));

        var result = await controller.Toggle(42);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(1, service.ToggleCallCount);
        Assert.Equal(456, service.LastAdminId);
    }

    private sealed class FixedCurrentAdminAccessor : ICurrentAdminAccessor
    {
        public FixedCurrentAdminAccessor(int memberId) => MemberID = memberId;

        public int MemberID { get; }
    }

    private sealed class RecordingTagService : ITagService
    {
        public int ToggleCallCount { get; private set; }
        public int? LastAdminId { get; private set; }

        public Task<TagsIndexViewModel> GetIndexAsync() => throw new NotSupportedException();

        public Task<(bool Success, string? Error)> CreateAsync(string name, int adminId)
            => throw new NotSupportedException();

        public Task<bool> ToggleAsync(int id, int adminId)
        {
            ToggleCallCount++;
            LastAdminId = adminId;
            return Task.FromResult(false);
        }

        public Task<bool> ReorderAsync(IReadOnlyList<int> orderedIds) => throw new NotSupportedException();
    }

    private sealed class RecordingRestaurantService : IRestaurantService
    {
        public int DisableCallCount { get; private set; }
        public int? LastAdminId { get; private set; }

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
            LastAdminId = byMemberId;
            return Task.FromResult(false);
        }

        public Task<bool> RestoreAsync(int id) => throw new NotSupportedException();
    }
}
