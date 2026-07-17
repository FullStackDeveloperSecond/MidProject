using MidProject.Models.DTOs;
using MidProject.Models;

namespace MidProject.Services;

public interface IReportService
{
    Task<PagedResult<ReportDto>> GetReportsAsync(ReportQueryParams query);
    Task<ReportDto?> GetByIdAsync(int reportId);
    Task<Report> CreateReportAsync(ReportCreateDto dto, int reporterMemberId);
    Task<bool> HandleReportAsync(int reportId, ReportHandleDto dto, int adminMemberId);
    Task<bool> NotifyReporterAsync(int reportId, NotifyReporterDto dto, int adminMemberId);
    Task<ReportDashboardDto> GetDashboardAsync();
}
