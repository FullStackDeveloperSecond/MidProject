using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidProject.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserLevels",
                columns: table => new
                {
                    LevelID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LevelName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinExp = table.Column<int>(type: "int", nullable: false),
                    Rewards = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLevels", x => x.LevelID);
                    table.CheckConstraint("CK_UserLevels_MinExp", "[MinExp] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "BusinessHours",
                columns: table => new
                {
                    BusinessHourID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestaurantID = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    CloseTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessHours", x => x.BusinessHourID);
                    table.CheckConstraint("CK_BusinessHours_DayOfWeek", "[DayOfWeek] >= 1 AND [DayOfWeek] <= 7");
                });

            migrationBuilder.CreateTable(
                name: "FavoriteFolders",
                columns: table => new
                {
                    FavoriteFolderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberID = table.Column<int>(type: "int", nullable: false),
                    FolderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteFolders", x => x.FavoriteFolderID);
                });

            migrationBuilder.CreateTable(
                name: "Favorites",
                columns: table => new
                {
                    FavoriteID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberID = table.Column<int>(type: "int", nullable: false),
                    RestaurantID = table.Column<int>(type: "int", nullable: false),
                    FavoriteFolderID = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Favorites", x => x.FavoriteID);
                    table.ForeignKey(
                        name: "FK_Favorites_FavoriteFolders_FavoriteFolderID",
                        column: x => x.FavoriteFolderID,
                        principalTable: "FavoriteFolders",
                        principalColumn: "FavoriteFolderID");
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    ImageID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadedByMemberID = table.Column<int>(type: "int", nullable: false),
                    ImageURL = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImageType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.ImageID);
                    table.CheckConstraint("CK_Images_ImageType", "[ImageType] IN ('RestaurantCover', 'RestaurantEnvironment', 'ReviewImage', 'MemberAvatar')");
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    MemberID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NickName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "User"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    AdminNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PenaltyEndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Birthday = table.Column<DateOnly>(type: "date", nullable: true),
                    LevelID = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Experience = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Points = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AvatarImageID = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.MemberID);
                    table.CheckConstraint("CK_Members_Experience", "[Experience] >= 0");
                    table.CheckConstraint("CK_Members_Points", "[Points] >= 0");
                    table.CheckConstraint("CK_Members_Role", "[Role] IN ('User', 'Admin')");
                    table.CheckConstraint("CK_Members_Status", "[Status] IN ('Normal', 'Warning', 'Muted', 'Suspended', 'Deleted')");
                    table.ForeignKey(
                        name: "FK_Members_Images_AvatarImageID",
                        column: x => x.AvatarImageID,
                        principalTable: "Images",
                        principalColumn: "ImageID");
                    table.ForeignKey(
                        name: "FK_Members_Members_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Members_UserLevels_LevelID",
                        column: x => x.LevelID,
                        principalTable: "UserLevels",
                        principalColumn: "LevelID");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberID = table.Column<int>(type: "int", nullable: true),
                    NotificationType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Personal"),
                    TargetRole = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TargetStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TargetLevelID = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsSent = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationID);
                    table.CheckConstraint("CK_Notifications_NotificationType", "[NotificationType] IN ('Personal', 'Condition')");
                    table.CheckConstraint("CK_Notifications_TargetRole", "[TargetRole] IS NULL OR [TargetRole] IN ('User', 'Admin')");
                    table.CheckConstraint("CK_Notifications_TargetStatus", "[TargetStatus] IS NULL OR [TargetStatus] IN ('Normal', 'Warning', 'Muted', 'Suspended', 'Deleted')");
                    table.ForeignKey(
                        name: "FK_Notifications_Members_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Notifications_Members_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Notifications_Members_MemberID",
                        column: x => x.MemberID,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Notifications_UserLevels_TargetLevelID",
                        column: x => x.TargetLevelID,
                        principalTable: "UserLevels",
                        principalColumn: "LevelID");
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                columns: table => new
                {
                    RestaurantID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    District = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DetailedAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    MemberID = table.Column<int>(type: "int", nullable: false),
                    AverageRating = table.Column<decimal>(type: "decimal(3,2)", nullable: false, defaultValue: 0m),
                    ReviewCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    DeleteReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.RestaurantID);
                    table.CheckConstraint("CK_Restaurants_AverageRating", "[AverageRating] >= 0 AND [AverageRating] <= 5");
                    table.CheckConstraint("CK_Restaurants_Latitude", "[Latitude] IS NULL OR ([Latitude] >= -90 AND [Latitude] <= 90)");
                    table.CheckConstraint("CK_Restaurants_Longitude", "[Longitude] IS NULL OR ([Longitude] >= -180 AND [Longitude] <= 180)");
                    table.CheckConstraint("CK_Restaurants_ReviewCount", "[ReviewCount] >= 0");
                    table.ForeignKey(
                        name: "FK_Restaurants_Members_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Restaurants_Members_MemberID",
                        column: x => x.MemberID,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    TagID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TagName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.TagID);
                    table.ForeignKey(
                        name: "FK_Tags_Members_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                });

            migrationBuilder.CreateTable(
                name: "RestaurantImages",
                columns: table => new
                {
                    RestaurantID = table.Column<int>(type: "int", nullable: false),
                    ImageID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantImages", x => new { x.RestaurantID, x.ImageID });
                    table.ForeignKey(
                        name: "FK_RestaurantImages_Images_ImageID",
                        column: x => x.ImageID,
                        principalTable: "Images",
                        principalColumn: "ImageID");
                    table.ForeignKey(
                        name: "FK_RestaurantImages_Restaurants_RestaurantID",
                        column: x => x.RestaurantID,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantID");
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    ReviewID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberID = table.Column<int>(type: "int", nullable: false),
                    RestaurantID = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    ReportCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.ReviewID);
                    table.CheckConstraint("CK_Reviews_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                    table.CheckConstraint("CK_Reviews_ReportCount", "[ReportCount] >= 0");
                    table.CheckConstraint("CK_Reviews_Status", "[Status] IN ('Active', 'PendingReview')");
                    table.ForeignKey(
                        name: "FK_Reviews_Members_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Reviews_Members_MemberID",
                        column: x => x.MemberID,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Reviews_Restaurants_RestaurantID",
                        column: x => x.RestaurantID,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantID");
                });

            migrationBuilder.CreateTable(
                name: "RestaurantTags",
                columns: table => new
                {
                    RestaurantID = table.Column<int>(type: "int", nullable: false),
                    TagID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantTags", x => new { x.RestaurantID, x.TagID });
                    table.ForeignKey(
                        name: "FK_RestaurantTags_Restaurants_RestaurantID",
                        column: x => x.RestaurantID,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantID");
                    table.ForeignKey(
                        name: "FK_RestaurantTags_Tags_TagID",
                        column: x => x.TagID,
                        principalTable: "Tags",
                        principalColumn: "TagID");
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    ReportID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporterMemberID = table.Column<int>(type: "int", nullable: false),
                    ReportedMemberID = table.Column<int>(type: "int", nullable: true),
                    RestaurantID = table.Column<int>(type: "int", nullable: true),
                    ReviewID = table.Column<int>(type: "int", nullable: true),
                    ImageID = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    HandledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HandledByMemberID = table.Column<int>(type: "int", nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.ReportID);
                    table.CheckConstraint("CK_Reports_Status", "[Status] IN ('Pending', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_Reports_Images_ImageID",
                        column: x => x.ImageID,
                        principalTable: "Images",
                        principalColumn: "ImageID");
                    table.ForeignKey(
                        name: "FK_Reports_Members_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Reports_Members_HandledByMemberID",
                        column: x => x.HandledByMemberID,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Reports_Members_ReportedMemberID",
                        column: x => x.ReportedMemberID,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Reports_Members_ReporterMemberID",
                        column: x => x.ReporterMemberID,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_Reports_Restaurants_RestaurantID",
                        column: x => x.RestaurantID,
                        principalTable: "Restaurants",
                        principalColumn: "RestaurantID");
                    table.ForeignKey(
                        name: "FK_Reports_Reviews_ReviewID",
                        column: x => x.ReviewID,
                        principalTable: "Reviews",
                        principalColumn: "ReviewID");
                });

            migrationBuilder.CreateTable(
                name: "ReviewImages",
                columns: table => new
                {
                    ReviewID = table.Column<int>(type: "int", nullable: false),
                    ImageID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewImages", x => new { x.ReviewID, x.ImageID });
                    table.ForeignKey(
                        name: "FK_ReviewImages_Images_ImageID",
                        column: x => x.ImageID,
                        principalTable: "Images",
                        principalColumn: "ImageID");
                    table.ForeignKey(
                        name: "FK_ReviewImages_Reviews_ReviewID",
                        column: x => x.ReviewID,
                        principalTable: "Reviews",
                        principalColumn: "ReviewID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessHours_RestaurantID",
                table: "BusinessHours",
                column: "RestaurantID");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteFolders_DeletedBy",
                table: "FavoriteFolders",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteFolders_IsDeleted",
                table: "FavoriteFolders",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteFolders_MemberID",
                table: "FavoriteFolders",
                column: "MemberID");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_DeletedBy",
                table: "Favorites",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_FavoriteFolderID",
                table: "Favorites",
                column: "FavoriteFolderID");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_IsDeleted",
                table: "Favorites",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_MemberID",
                table: "Favorites",
                column: "MemberID");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_RestaurantID",
                table: "Favorites",
                column: "RestaurantID");

            migrationBuilder.CreateIndex(
                name: "IX_Images_DeletedBy",
                table: "Images",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Images_ImageType",
                table: "Images",
                column: "ImageType");

            migrationBuilder.CreateIndex(
                name: "IX_Images_IsDeleted",
                table: "Images",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Images_UploadedByMemberID",
                table: "Images",
                column: "UploadedByMemberID");

            migrationBuilder.CreateIndex(
                name: "IX_Members_AvatarImageID",
                table: "Members",
                column: "AvatarImageID");

            migrationBuilder.CreateIndex(
                name: "IX_Members_DeletedBy",
                table: "Members",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Members_Email",
                table: "Members",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_IsDeleted",
                table: "Members",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Members_LevelID",
                table: "Members",
                column: "LevelID");

            migrationBuilder.CreateIndex(
                name: "IX_Members_UserName",
                table: "Members",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedBy",
                table: "Notifications",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DeletedBy",
                table: "Notifications",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsDeleted",
                table: "Notifications",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsSent",
                table: "Notifications",
                column: "IsSent");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_MemberID",
                table: "Notifications",
                column: "MemberID");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_NotificationType",
                table: "Notifications",
                column: "NotificationType");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TargetLevelID",
                table: "Notifications",
                column: "TargetLevelID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_DeletedBy",
                table: "Reports",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_HandledByMemberID",
                table: "Reports",
                column: "HandledByMemberID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ImageID",
                table: "Reports",
                column: "ImageID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_IsDeleted",
                table: "Reports",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportedMemberID",
                table: "Reports",
                column: "ReportedMemberID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterMemberID",
                table: "Reports",
                column: "ReporterMemberID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_RestaurantID",
                table: "Reports",
                column: "RestaurantID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReviewID",
                table: "Reports",
                column: "ReviewID");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status",
                table: "Reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantImages_ImageID",
                table: "RestaurantImages",
                column: "ImageID");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_City",
                table: "Restaurants",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_DeletedBy",
                table: "Restaurants",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_District",
                table: "Restaurants",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_IsDeleted",
                table: "Restaurants",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_MemberID",
                table: "Restaurants",
                column: "MemberID");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTags_TagID",
                table: "RestaurantTags",
                column: "TagID");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewImages_ImageID",
                table: "ReviewImages",
                column: "ImageID");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_DeletedBy",
                table: "Reviews",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_IsDeleted",
                table: "Reviews",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_MemberID",
                table: "Reviews",
                column: "MemberID");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_RestaurantID",
                table: "Reviews",
                column: "RestaurantID");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Status",
                table: "Reviews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_DeletedBy",
                table: "Tags",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_IsDeleted",
                table: "Tags",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_TagName",
                table: "Tags",
                column: "TagName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_LevelName",
                table: "UserLevels",
                column: "LevelName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLevels_MinExp",
                table: "UserLevels",
                column: "MinExp",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessHours_Restaurants_RestaurantID",
                table: "BusinessHours",
                column: "RestaurantID",
                principalTable: "Restaurants",
                principalColumn: "RestaurantID");

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteFolders_Members_DeletedBy",
                table: "FavoriteFolders",
                column: "DeletedBy",
                principalTable: "Members",
                principalColumn: "MemberID");

            migrationBuilder.AddForeignKey(
                name: "FK_FavoriteFolders_Members_MemberID",
                table: "FavoriteFolders",
                column: "MemberID",
                principalTable: "Members",
                principalColumn: "MemberID");

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Members_DeletedBy",
                table: "Favorites",
                column: "DeletedBy",
                principalTable: "Members",
                principalColumn: "MemberID");

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Members_MemberID",
                table: "Favorites",
                column: "MemberID",
                principalTable: "Members",
                principalColumn: "MemberID");

            migrationBuilder.AddForeignKey(
                name: "FK_Favorites_Restaurants_RestaurantID",
                table: "Favorites",
                column: "RestaurantID",
                principalTable: "Restaurants",
                principalColumn: "RestaurantID");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Members_DeletedBy",
                table: "Images",
                column: "DeletedBy",
                principalTable: "Members",
                principalColumn: "MemberID");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Members_UploadedByMemberID",
                table: "Images",
                column: "UploadedByMemberID",
                principalTable: "Members",
                principalColumn: "MemberID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Members_DeletedBy",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Members_UploadedByMemberID",
                table: "Images");

            migrationBuilder.DropTable(
                name: "BusinessHours");

            migrationBuilder.DropTable(
                name: "Favorites");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "RestaurantImages");

            migrationBuilder.DropTable(
                name: "RestaurantTags");

            migrationBuilder.DropTable(
                name: "ReviewImages");

            migrationBuilder.DropTable(
                name: "FavoriteFolders");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "Restaurants");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "UserLevels");
        }
    }
}
