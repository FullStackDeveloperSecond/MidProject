using MidProject.Models.ViewModels.Restaurants;

namespace MidProject.Services.IServices;

public interface IRestaurantService
{
    Task<RestaurantIndexViewModel> GetIndexAsync(RestaurantFilterQuery filter);
    Task<RestaurantDeletedIndexViewModel> GetDeletedIndexAsync(RestaurantDeletedFilterQuery filter);
    Task<RestaurantDetailViewModel?> GetDetailAsync(int id);
    Task<RestaurantFormViewModel> GetCreateFormAsync();
    Task<RestaurantFormViewModel?> GetEditFormAsync(int id);
    Task<(bool Success, int? NewId)> CreateAsync(RestaurantFormViewModel form, int adminId);
    Task<bool> EditAsync(int id, RestaurantFormViewModel form, int adminId);
    Task<RestaurantFormViewModel> RehydrateFormAsync(RestaurantFormViewModel form);
    Task<bool> DisableAsync(int id, string reason, int? byMemberId = null);
    Task<bool> RestoreAsync(int id);
}
