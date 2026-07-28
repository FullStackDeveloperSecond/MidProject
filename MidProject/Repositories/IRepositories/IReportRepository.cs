using MidProject.Models.DTOs;
using MidProject.Models;

namespace MidProject.Repositories;

public interface IReportRepository
{
    Task<PagedResult<Report>> GetReportsAsync(ReportQueryParams query);
    Task<Report?> GetByIdAsync(int reportId);
    Task AddAsync(Report report);
    Task SaveChangesAsync();

    // Dashboard 用
    Task<(int pending, int approved, int rejected)> GetStatusCountsAsync();
    Task<int> GetPendingCountSinceAsync(DateTime since);
    Task<List<ReportStatusDatePoint>> GetStatusDatesSinceAsync(DateTime since);
}
