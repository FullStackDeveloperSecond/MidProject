using MidProject.Models;
using MidProject.Models.ViewModels.Restaurants;

namespace MidProject.Repositories.IRepositories;

public interface IRestaurantRepository
{
    Task<(List<Restaurant> Items, int TotalCount)> GetActivePagedAsync(RestaurantFilterQuery filter, int pageSize);
    Task<(List<Restaurant> Items, int TotalCount)> GetDeletedPagedAsync(RestaurantDeletedFilterQuery filter, int pageSize);
    Task<Restaurant?> GetMostRecentlyDeletedAsync(RestaurantDeletedFilterQuery filter);
    Task<Restaurant?> GetByIdAsync(int id);
    Task AddAsync(Restaurant restaurant);
    Task ReplaceBusinessHoursAsync(int restaurantId, List<BusinessHour> hours);
    Task ReplaceTagsAsync(int restaurantId, List<int> tagIds);
    Task SetCoverImageAsync(int restaurantId, Image newCoverImage);
    Task RemoveCoverImageAsync(int restaurantId);
    Task AddEnvironmentImageAsync(int restaurantId, Image image);
    Task RemoveEnvironmentImageAsync(int restaurantId, int imageId);
    Task SoftDeleteAsync(int id, string reason, int byMemberId);
    Task RestoreAsync(int id);
    Task<int> GetDefaultAdminMemberIdAsync();
    Task<RestaurantStats> GetStatsAsync(RestaurantFilterQuery filter);
    Task<IReadOnlyDictionary<int, RestaurantReviewStats>> GetReviewStatsAsync(IEnumerable<int> restaurantIds);
    Task<List<string>> GetDistinctCitiesAsync();
    Task<List<string>> GetDistinctDistrictsAsync();
    Task<List<string>> GetDistinctDeleteReasonsAsync();
    Task SaveChangesAsync();
}
