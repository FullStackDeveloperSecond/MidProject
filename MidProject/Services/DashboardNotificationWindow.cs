using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class DashboardNotificationWindow : IDashboardNotificationWindow
{
    private readonly INotificationRepository _repository;
    private readonly ITaipeiClock _clock;
    private readonly ILogger<DashboardNotificationWindow> _logger;

    public DashboardNotificationWindow(
        INotificationRepository repository,
        ITaipeiClock clock,
        ILogger<DashboardNotificationWindow> logger)
    {
        _repository = repository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<DashboardNotificationSummaryResult> GetPendingSummaryAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        try
        {
            var count = await _repository.CountPendingAsync(cancellationToken);
            return new(
                DashboardNotificationClassification.Success,
                count,
                "Notifications",
                "Index",
                new Dictionary<string, object> { ["isSent"] = false },
                null,
                correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Dashboard notification query failed at {TaipeiTimestamp}; Operation=DashboardPendingCount; ResultClassification=Failed; CorrelationID={CorrelationID}; ExceptionType={ExceptionType}",
                _clock.GetNow(), correlationId, ex.GetType().Name);
            return new(
                DashboardNotificationClassification.Failed,
                null,
                "Notifications",
                "Index",
                new Dictionary<string, object> { ["isSent"] = false },
                "QUERY_FAILED",
                correlationId);
        }
    }
}
