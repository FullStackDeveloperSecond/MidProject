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

    Task<PointsStoreRedeemResult> RedeemAsync(
        int memberId,
        int frameId,
        CancellationToken cancellationToken = default);
}

public enum PointsStoreRedeemClassification
{
    Redeemed,
    MemberNotFound,
    FrameNotFound,
    FrameUnavailable,
    InvalidFramePrice,
    AlreadyOwned,
    InsufficientPoints,
    Failed
}

public sealed record PointsStoreRedeemResult(
    PointsStoreRedeemClassification Classification,
    int? BalanceAfter = null,
    int? TransactionID = null,
    string? SafeErrorCode = null);
