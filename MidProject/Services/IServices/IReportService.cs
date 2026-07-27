using MidProject.Models.DTOs;
using MidProject.Models;

namespace MidProject.Services;

public interface IReportService
{
    Task<PagedResult<ReportDto>> GetReportsAsync(ReportQueryParams query);
    Task<ReportDto?> GetByIdAsync(int reportId);
    Task<Report> CreateReportAsync(ReportCreateDto dto, int reporterMemberId);
    Task<ReportHandleOutcome> HandleReportAsync(int reportId, ReportHandleDto dto, int adminMemberId);
    Task<ReportNotifyResult> NotifyReporterAsync(int reportId, NotifyReporterDto dto, int adminMemberId);
    Task<ReportNotifyResult> NotifyReportedMemberAsync(int reportId, NotifyReporterDto dto, int adminMemberId);
    Task<List<ReportNotificationRecordDto>> GetSentNotificationsAsync(int reportId);
    Task<ReportDashboardDto> GetDashboardAsync();
}
