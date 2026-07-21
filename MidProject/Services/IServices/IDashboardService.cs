using MidProject.Models.ViewModels.Dashboard;

namespace MidProject.Services.IServices;

public interface IDashboardService
{
    Task<DashboardIndexViewModel> GetIndexAsync(CancellationToken cancellationToken = default);

    Task<DashboardDetailsViewModel?> GetDetailsAsync(
        string type,
        CancellationToken cancellationToken = default);
}
