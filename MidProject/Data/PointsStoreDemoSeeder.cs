using System.Data;
using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Services.IServices;

namespace MidProject.Data;

public static class PointsStoreDemoSeeder
{
    private static readonly FrameSeed[] Frames =
    {
        new("Demo Common 外框", "Common", 50, 1),
        new("Demo Rare 外框", "Rare", 100, 2),
        new("Demo Limited 外框", "Limited", 200, 3)
    };

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ITaipeiClock>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MidProject.Data.PointsStoreDemoSeeder");
        var now = clock.GetNow();

        await SeedFramesAsync(context, now);

        var redemptions = new[]
        {
            new RedemptionSeed("aiden@example.com", "Demo Common 外框"),
            new RedemptionSeed("admin@example.com", "Demo Rare 外框"),
            new RedemptionSeed("admin@example.com", "Demo Limited 外框")
        };
        foreach (var redemption in redemptions)
        {
            await SeedRedemptionAsync(context, redemption, now, logger);
        }
    }

    public static async Task SeedFramesAsync(AppDbContext context, DateTime now)
    {
        var existingFrames = await context.AvatarFrames
            .Where(frame => Frames.Select(item => item.Name).Contains(frame.Name))
            .ToDictionaryAsync(frame => frame.Name);

        foreach (var frame in Frames)
        {
            if (existingFrames.TryGetValue(frame.Name, out var existingFrame))
            {
                existingFrame.Rarity = frame.Rarity;
                existingFrame.PointsPrice = frame.PointsPrice;
                existingFrame.SortOrder = frame.SortOrder;
                existingFrame.IsActive = true;
                existingFrame.IsDeleted = false;
                existingFrame.DeletedAt = null;
                existingFrame.DeletedBy = null;
                existingFrame.UpdatedAt = now;
                continue;
            }

            context.AvatarFrames.Add(new AvatarFrame
            {
                Name = frame.Name,
                Description = "SeedData 產生的點數商城示範商品。",
                Rarity = frame.Rarity,
                PointsPrice = frame.PointsPrice,
                SortOrder = frame.SortOrder,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRedemptionAsync(
        AppDbContext context,
        RedemptionSeed definition,
        DateTime now,
        ILogger logger)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable);
        var member = await context.Members.SingleOrDefaultAsync(
            item => item.Email == definition.MemberEmail && !item.IsDeleted);
        var frame = await context.AvatarFrames.SingleOrDefaultAsync(
            item => item.Name == definition.FrameName && !item.IsDeleted);
        if (member is null || frame is null)
        {
            logger.LogWarning(
                "Point-store demo redemption skipped; MemberEmail={MemberEmail}; FrameName={FrameName}; SafeErrorCode={SafeErrorCode}",
                definition.MemberEmail,
                definition.FrameName,
                "DEMO_REFERENCE_NOT_FOUND");
            return;
        }

        var ownership = await context.MemberAvatarFrames.SingleOrDefaultAsync(
            item => item.MemberID == member.MemberID && item.FrameID == frame.FrameID);
        var pointsTransaction = await context.PointsTransactions.SingleOrDefaultAsync(
            item => item.MemberID == member.MemberID &&
                    item.RelatedFrameID == frame.FrameID &&
                    item.Type == "Redeem");

        if (ownership is not null && pointsTransaction is not null)
        {
            NormalizeTransaction(pointsTransaction, member, frame);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return;
        }

        if (ownership is null && pointsTransaction is null)
        {
            if (member.Points < frame.PointsPrice)
            {
                logger.LogWarning(
                    "Point-store demo redemption skipped for insufficient points; MemberID={MemberID}; FrameID={FrameID}; SafeErrorCode={SafeErrorCode}",
                    member.MemberID,
                    frame.FrameID,
                    "DEMO_INSUFFICIENT_POINTS");
                return;
            }

            member.Points -= frame.PointsPrice;
            member.UpdatedAt = now;
            ownership = CreateOwnership(member.MemberID, frame.FrameID, now);
            pointsTransaction = CreateTransaction(member, frame, now);
            context.MemberAvatarFrames.Add(ownership);
            context.PointsTransactions.Add(pointsTransaction);
        }
        else if (ownership is null)
        {
            NormalizeTransaction(pointsTransaction!, member, frame);
            context.MemberAvatarFrames.Add(
                CreateOwnership(member.MemberID, frame.FrameID, pointsTransaction!.CreatedAt));
        }
        else
        {
            context.PointsTransactions.Add(CreateTransaction(member, frame, ownership.RedeemedAt));
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static void NormalizeTransaction(
        PointsTransaction pointsTransaction,
        Member member,
        AvatarFrame frame)
    {
        pointsTransaction.Amount = -frame.PointsPrice;
        pointsTransaction.BalanceAfter = member.Points;
        pointsTransaction.CreatedBy = null;
        pointsTransaction.Note = $"示範兌換外框：{frame.Name}";
    }

    private static MemberAvatarFrame CreateOwnership(
        int memberId,
        int frameId,
        DateTime redeemedAt)
    {
        return new MemberAvatarFrame
        {
            MemberID = memberId,
            FrameID = frameId,
            RedeemedAt = redeemedAt
        };
    }

    private static PointsTransaction CreateTransaction(
        Member member,
        AvatarFrame frame,
        DateTime createdAt)
    {
        return new PointsTransaction
        {
            MemberID = member.MemberID,
            Amount = -frame.PointsPrice,
            BalanceAfter = member.Points,
            Type = "Redeem",
            RelatedFrameID = frame.FrameID,
            Note = $"示範兌換外框：{frame.Name}",
            CreatedAt = createdAt,
            CreatedBy = null
        };
    }

    private sealed record FrameSeed(
        string Name,
        string Rarity,
        int PointsPrice,
        int SortOrder);

    private sealed record RedemptionSeed(string MemberEmail, string FrameName);
}
