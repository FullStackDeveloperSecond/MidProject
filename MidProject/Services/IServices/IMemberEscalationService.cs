namespace MidProject.Services.IServices;

// 自動懲處批次邏輯的對外介面。同時被兩種呼叫方使用：
// 1. MemberEscalationBackgroundService：每 30 秒的排程安全網。
// 2. AdminMembersController 的「立即重新檢查」按鈕：管理員需要馬上看到結果時手動觸發。
// 兩邊共用同一份實作，確保排程跟手動觸發算出來的結果一致，不會有兩套邏輯。
public interface IMemberEscalationService
{
    Task<MemberEscalationRunResult> RunOnceAsync(CancellationToken cancellationToken = default);
}

public readonly record struct MemberEscalationRunResult(int ExpiredResetCount, int EscalatedCount);
