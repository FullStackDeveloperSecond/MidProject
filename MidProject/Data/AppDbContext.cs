using Microsoft.EntityFrameworkCore;
using MidProject.Models;

namespace MidProject.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UserLevel> UserLevels => Set<UserLevel>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<RestaurantTag> RestaurantTags => Set<RestaurantTag>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<RestaurantImage> RestaurantImages => Set<RestaurantImage>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewImage> ReviewImages => Set<ReviewImage>();
    public DbSet<FavoriteFolder> FavoriteFolders => Set<FavoriteFolder>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AvatarFrame> AvatarFrames => Set<AvatarFrame>();
    public DbSet<MemberAvatarFrame> MemberAvatarFrames => Set<MemberAvatarFrame>();
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserLevel>(entity =>
        {
            entity.HasKey(e => e.LevelID);
            entity.HasIndex(e => e.LevelName).IsUnique();
            entity.HasIndex(e => e.MinExp).IsUnique();
            entity.Property(e => e.LevelName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Rewards).HasMaxLength(50);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_UserLevels_MinExp", "[MinExp] >= 0"));
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.HasKey(e => e.MemberID);
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.UserName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NickName).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Role).HasMaxLength(10).IsRequired().HasDefaultValue("User");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsLocked).HasDefaultValue(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Normal");
            entity.Property(e => e.WarningCount).HasDefaultValue(0);
            entity.Property(e => e.FailedLoginCount).HasDefaultValue(0);
            entity.Property(e => e.LoginLockoutEndAt);
            entity.Property(e => e.LevelID).HasDefaultValue(1);
            entity.Property(e => e.Experience).HasDefaultValue(0);
            entity.Property(e => e.Points).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Members_Role", "[Role] IN ('User', 'Admin')");
                table.HasCheckConstraint("CK_Members_Status", "[Status] IN ('Normal', 'Warning', 'Muted', 'Suspended', 'Deleted')");
                table.HasCheckConstraint("CK_Members_WarningCount", "[WarningCount] >= 0");
                table.HasCheckConstraint("CK_Members_FailedLoginCount", "[FailedLoginCount] >= 0");
                table.HasCheckConstraint("CK_Members_Experience", "[Experience] >= 0");
                table.HasCheckConstraint("CK_Members_Points", "[Points] >= 0");
            });
            entity.HasOne(e => e.UserLevel).WithMany(e => e.Members).HasForeignKey(e => e.LevelID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.AvatarImage).WithMany().HasForeignKey(e => e.AvatarImageID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.EquippedFrame).WithMany().HasForeignKey(e => e.EquippedFrameID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.HasKey(e => e.RestaurantID);
            entity.HasIndex(e => e.City);
            entity.HasIndex(e => e.District);
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.City).HasMaxLength(20).IsRequired();
            entity.Property(e => e.District).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DetailedAddress).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Latitude).HasColumnType("decimal(9,6)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(9,6)");
            entity.Property(e => e.AverageRating).HasColumnType("decimal(3,2)").HasDefaultValue(0m);
            entity.Property(e => e.ReviewCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.DeleteReason).HasMaxLength(200);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Restaurants_Latitude", "[Latitude] IS NULL OR ([Latitude] >= -90 AND [Latitude] <= 90)");
                table.HasCheckConstraint("CK_Restaurants_Longitude", "[Longitude] IS NULL OR ([Longitude] >= -180 AND [Longitude] <= 180)");
                table.HasCheckConstraint("CK_Restaurants_AverageRating", "[AverageRating] >= 0 AND [AverageRating] <= 5");
                table.HasCheckConstraint("CK_Restaurants_ReviewCount", "[ReviewCount] >= 0");
            });
            entity.HasOne(e => e.Member).WithMany(e => e.Restaurants).HasForeignKey(e => e.MemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<BusinessHour>(entity =>
        {
            entity.HasKey(e => e.BusinessHourID);
            entity.ToTable(table => table.HasCheckConstraint("CK_BusinessHours_DayOfWeek", "[DayOfWeek] >= 1 AND [DayOfWeek] <= 7"));
            entity.Property(e => e.OpenTime).HasColumnType("time(0)");
            entity.Property(e => e.CloseTime).HasColumnType("time(0)");
            entity.Property(e => e.IsClosed).HasDefaultValue(false);
            entity.HasOne(e => e.Restaurant).WithMany(e => e.BusinessHours).HasForeignKey(e => e.RestaurantID).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.TagID);
            entity.HasIndex(e => e.TagName).IsUnique();
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.TagName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RestaurantTag>(entity =>
        {
            entity.HasKey(e => new { e.RestaurantID, e.TagID });
            entity.HasOne(e => e.Restaurant).WithMany(e => e.RestaurantTags).HasForeignKey(e => e.RestaurantID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Tag).WithMany(e => e.RestaurantTags).HasForeignKey(e => e.TagID).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.ImageID);
            entity.HasIndex(e => e.ImageType);
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.ImageURL).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ImageType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_Images_ImageType", "[ImageType] IN ('RestaurantCover', 'RestaurantEnvironment', 'ReviewImage', 'MemberAvatar', 'AvatarFrame')"));
            entity.HasOne(e => e.UploadedByMember).WithMany(e => e.UploadedImages).HasForeignKey(e => e.UploadedByMemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RestaurantImage>(entity =>
        {
            entity.HasKey(e => new { e.RestaurantID, e.ImageID });
            entity.HasOne(e => e.Restaurant).WithMany(e => e.RestaurantImages).HasForeignKey(e => e.RestaurantID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Image).WithMany(e => e.RestaurantImages).HasForeignKey(e => e.ImageID).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewID);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
            entity.Property(e => e.ReportCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Reviews_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                table.HasCheckConstraint("CK_Reviews_Status", "[Status] IN ('Active', 'PendingReview')");
                table.HasCheckConstraint("CK_Reviews_ReportCount", "[ReportCount] >= 0");
            });
            entity.HasOne(e => e.Member).WithMany(e => e.Reviews).HasForeignKey(e => e.MemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Restaurant).WithMany(e => e.Reviews).HasForeignKey(e => e.RestaurantID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ReviewImage>(entity =>
        {
            entity.HasKey(e => new { e.ReviewID, e.ImageID });
            entity.HasOne(e => e.Review).WithMany(e => e.ReviewImages).HasForeignKey(e => e.ReviewID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Image).WithMany(e => e.ReviewImages).HasForeignKey(e => e.ImageID).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<FavoriteFolder>(entity =>
        {
            entity.HasKey(e => e.FavoriteFolderID);
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.FolderName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasOne(e => e.Member).WithMany(e => e.FavoriteFolders).HasForeignKey(e => e.MemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => e.FavoriteID);
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasOne(e => e.Member).WithMany(e => e.Favorites).HasForeignKey(e => e.MemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Restaurant).WithMany(e => e.Favorites).HasForeignKey(e => e.RestaurantID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.FavoriteFolder).WithMany(e => e.Favorites).HasForeignKey(e => e.FavoriteFolderID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.ReportID);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.Category).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_Reports_Status", "[Status] IN ('Pending', 'Approved', 'Rejected')"));
            entity.HasOne(e => e.ReporterMember).WithMany().HasForeignKey(e => e.ReporterMemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.ReportedMember).WithMany().HasForeignKey(e => e.ReportedMemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Restaurant).WithMany().HasForeignKey(e => e.RestaurantID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Review).WithMany(e => e.Reports).HasForeignKey(e => e.ReviewID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImageID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.HandledByMember).WithMany().HasForeignKey(e => e.HandledByMemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AvatarFrame>(entity =>
        {
            entity.HasKey(e => e.FrameID);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.IsDeleted);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Rarity).HasMaxLength(10).IsRequired().HasDefaultValue("Common");
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_AvatarFrames_Rarity", "[Rarity] IN ('Common', 'Rare', 'Limited')");
                table.HasCheckConstraint("CK_AvatarFrames_PointsPrice", "[PointsPrice] >= 0");
            });
            entity.HasOne(e => e.Image).WithMany(e => e.AvatarFrames).HasForeignKey(e => e.ImageID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<MemberAvatarFrame>(entity =>
        {
            entity.HasKey(e => e.MemberAvatarFrameID);
            entity.HasIndex(e => new { e.MemberID, e.FrameID }).IsUnique();
            entity.Property(e => e.RedeemedAt).HasDefaultValueSql("GETDATE()");
            entity.HasOne(e => e.Member).WithMany(e => e.MemberAvatarFrames).HasForeignKey(e => e.MemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Frame).WithMany(e => e.MemberAvatarFrames).HasForeignKey(e => e.FrameID).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PointsTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionID);
            entity.HasIndex(e => new { e.MemberID, e.CreatedAt });
            entity.HasIndex(e => e.Type);
            entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_PointsTransactions_Type", "[Type] IN ('Redeem', 'AdminAdjust', 'Earn')");
                table.HasCheckConstraint("CK_PointsTransactions_RelatedFrame", "([Type] = 'Redeem' AND [RelatedFrameID] IS NOT NULL) OR ([Type] <> 'Redeem' AND [RelatedFrameID] IS NULL)");
                table.HasCheckConstraint("CK_PointsTransactions_BalanceAfter", "[BalanceAfter] >= 0");
                table.HasCheckConstraint("CK_PointsTransactions_AmountByType", "([Type] = 'Redeem' AND [Amount] < 0) OR ([Type] = 'Earn' AND [Amount] > 0) OR ([Type] = 'AdminAdjust' AND [Amount] <> 0)");
                table.HasCheckConstraint("CK_PointsTransactions_CreatedByType", "([Type] = 'AdminAdjust' AND [CreatedBy] IS NOT NULL) OR ([Type] IN ('Redeem', 'Earn') AND [CreatedBy] IS NULL)");
            });
            entity.HasOne(e => e.Member).WithMany(e => e.PointsTransactions).HasForeignKey(e => e.MemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.RelatedFrame).WithMany(e => e.PointsTransactions).HasForeignKey(e => e.RelatedFrameID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.CreatedByMember).WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationID);
            entity.HasIndex(e => e.NotificationType);
            entity.HasIndex(e => new { e.IsDeleted, e.ScheduledAt, e.NotificationID });
            entity.HasIndex(e => new { e.IsSent, e.IsDeleted, e.ScheduledAt, e.NotificationID });
            // 2026/07: 去重粒度改為（來源檢舉＋結果＋收件會員）複合唯一——
            // 同一檢舉＋同一結果對同一位收件人只會有一筆通知（避免重複通知），
            // 但「通知檢舉者」與「通知被檢舉會員」因 MemberID 不同仍可各存一筆。
            entity.HasIndex(e => new { e.SourceReportID, e.SourceReportOutcome, e.MemberID })
                .IsUnique()
                .HasFilter("[SourceReportID] IS NOT NULL AND [SourceReportOutcome] IS NOT NULL");
            entity.Property(e => e.NotificationType).HasMaxLength(20).IsRequired().HasDefaultValue("Personal");
            entity.Property(e => e.TargetRole).HasMaxLength(10);
            entity.Property(e => e.TargetStatus).HasMaxLength(20);
            entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Content).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.IsSent).HasDefaultValue(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.SourceReportOutcome).HasMaxLength(10);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Notifications_NotificationType", "[NotificationType] IN ('Personal', 'Condition')");
                table.HasCheckConstraint("CK_Notifications_TargetRole", "[TargetRole] IS NULL OR [TargetRole] IN ('User', 'Admin')");
                table.HasCheckConstraint("CK_Notifications_TargetStatus", "[TargetStatus] IS NULL OR [TargetStatus] IN ('Normal', 'Warning', 'Muted', 'Suspended', 'Deleted')");
                table.HasCheckConstraint("CK_Notifications_Audience", "([NotificationType] = 'Personal' AND [MemberID] IS NOT NULL AND [TargetRole] IS NULL AND [TargetStatus] IS NULL AND [TargetLevelID] IS NULL) OR ([NotificationType] = 'Condition' AND [MemberID] IS NULL)");
                table.HasCheckConstraint("CK_Notifications_ReportSource", "([SourceReportID] IS NULL AND [SourceReportOutcome] IS NULL) OR ([SourceReportID] IS NOT NULL AND [SourceReportOutcome] IN ('Approved', 'Rejected'))");
                table.HasCheckConstraint("CK_Notifications_SentState", "([IsSent] = 0 AND [SentAt] IS NULL) OR ([IsSent] = 1 AND [SentAt] IS NOT NULL)");
                table.HasCheckConstraint("CK_Notifications_DeletedState", "([IsDeleted] = 0 AND [DeletedAt] IS NULL AND [DeletedBy] IS NULL) OR ([IsDeleted] = 1 AND [IsSent] = 0 AND [DeletedAt] IS NOT NULL AND [DeletedBy] IS NOT NULL)");
            });
            entity.HasOne(e => e.Member).WithMany().HasForeignKey(e => e.MemberID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.TargetLevel).WithMany(e => e.Notifications).HasForeignKey(e => e.TargetLevelID).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.CreatedByMember).WithMany().HasForeignKey(e => e.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.DeletedByMember).WithMany().HasForeignKey(e => e.DeletedBy).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.SourceReport).WithMany(e => e.Notifications).HasForeignKey(e => e.SourceReportID).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
