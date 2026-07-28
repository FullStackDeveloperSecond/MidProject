using MidProject.Services.IServices;

namespace MidProject.Services;

// 自動懲處的排程安全網：每 30 秒（含應用程式啟動時立即跑一次）呼叫一次 IMemberEscalationService，
// 確保就算沒有人手動按「立即重新檢查」，懲處狀態也不會拖太久沒更新。
// 實際批次邏輯在 MemberEscalationService；這裡只負責計時迴圈跟每次建立獨立的 DI scope
// （BackgroundService 是 Singleton 生命週期，AppDbContext 是 Scoped，不能直接注入，要每次自己開 scope）。
public sealed class MemberEscalationBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemberEscalationBackgroundService> _logger;

    public MemberEscalationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MemberEscalationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var escalationService = scope.ServiceProvider.GetRequiredService<IMemberEscalationService>();
                var result = await escalationService.RunOnceAsync(stoppingToken);
                LogRun(result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "自動懲處排程執行失敗");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 應用程式關閉，正常結束迴圈
            }
        }
    }

    private void LogRun(MemberEscalationRunResult result)
    {
        if (result.ExpiredResetCount == 0 && result.EscalatedCount == 0) return;

        _logger.LogInformation(
            "自動懲處排程執行完成；處分期限到期恢復正常人數={ExpiredResetCount}；套用新懲處人數={EscalatedCount}",
            result.ExpiredResetCount, result.EscalatedCount);
    }
}
