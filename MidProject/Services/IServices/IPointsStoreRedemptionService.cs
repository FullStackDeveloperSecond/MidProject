using MidProject.Models.ViewModels.PointsStore;

namespace MidProject.Services.IServices;

public interface IPointsStoreRedemptionService
{
    Task<RedemptionsIndexViewModel> GetIndexAsync(
        string? keyword,
        int? frameFilter,
        DateOnly? startDate,
        DateOnly? endDate,
        int page = 1);
}
