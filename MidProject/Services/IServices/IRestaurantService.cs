using MidProject.Models.ViewModels.Restaurants;

namespace MidProject.Services.IServices;

public interface IRestaurantService
{
    Task<RestaurantIndexViewModel> GetIndexAsync(RestaurantFilterQuery filter);
    Task<RestaurantDeletedIndexViewModel> GetDeletedIndexAsync(RestaurantDeletedFilterQuery filter);
    Task<RestaurantDetailViewModel?> GetDetailAsync(int id);
    Task<RestaurantFormViewModel> GetCreateFormAsync();
    Task<RestaurantFormViewModel?> GetEditFormAsync(int id);
    Task<(bool Success, int? NewId, string? Error)> CreateAsync(RestaurantFormViewModel form);
    Task<(bool Success, string? Error)> EditAsync(int id, RestaurantFormViewModel form);
    Task<RestaurantFormViewModel> RehydrateFormAsync(RestaurantFormViewModel form);
    Task DisableAsync(int id, string reason);
    Task RestoreAsync(int id);
}
