using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidProject.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarFrameRedemptionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Images_ImageType",
                table: "Images");

            migrationBuilder.AddColumn<int>(
                name: "EquippedFrameID",
                table: "Members",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AvatarFrames",
                columns: table => new
                {
                    FrameID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Rarity = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "Common"),
                    PointsPrice = table.Column<int>(type: "int", nullable: false),
                    ImageID = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvatarFrames", x => x.FrameID);
                    table.CheckConstraint("CK_AvatarFrames_Rarity", "[Rarity] IN ('Common', 'Rare', 'Limited')");
                    table.ForeignKey(
                        name: "FK_AvatarFrames_Images_ImageID",
                        column: x => x.ImageID,
                        principalTable: "Images",
                        principalColumn: "ImageID");
                    table.ForeignKey(
                        name: "FK_AvatarFrames_Members_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                });

            migrationBuilder.CreateTable(
                name: "MemberAvatarFrames",
                columns: table => new
                {
                    MemberAvatarFrameID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberID = table.Column<int>(type: "int", nullable: false),
                    FrameID = table.Column<int>(type: "int", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberAvatarFrames", x => x.MemberAvatarFrameID);
                    table.ForeignKey(
                        name: "FK_MemberAvatarFrames_AvatarFrames_FrameID",
                        column: x => x.FrameID,
                        principalTable: "AvatarFrames",
                        principalColumn: "FrameID");
                    table.ForeignKey(
                        name: "FK_MemberAvatarFrames_Members_MemberID",
                        column: x => x.MemberID,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                });

            migrationBuilder.CreateTable(
                name: "PointsTransactions",
                columns: table => new
                {
                    TransactionID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MemberID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RelatedFrameID = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsTransactions", x => x.TransactionID);
                    table.CheckConstraint("CK_PointsTransactions_BalanceAfter", "[BalanceAfter] >= 0");
                    table.CheckConstraint("CK_PointsTransactions_RelatedFrame", "([Type] = 'Redeem' AND [RelatedFrameID] IS NOT NULL) OR ([Type] <> 'Redeem' AND [RelatedFrameID] IS NULL)");
                    table.CheckConstraint("CK_PointsTransactions_Type", "[Type] IN ('Redeem', 'AdminAdjust', 'Earn')");
                    table.ForeignKey(
                        name: "FK_PointsTransactions_AvatarFrames_RelatedFrameID",
                        column: x => x.RelatedFrameID,
                        principalTable: "AvatarFrames",
                        principalColumn: "FrameID");
                    table.ForeignKey(
                        name: "FK_PointsTransactions_Members_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                    table.ForeignKey(
                        name: "FK_PointsTransactions_Members_MemberID",
                        column: x => x.MemberID,
                        principalTable: "Members",
                        principalColumn: "MemberID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_EquippedFrameID",
                table: "Members",
                column: "EquippedFrameID");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Images_ImageType",
                table: "Images",
                sql: "[ImageType] IN ('RestaurantCover', 'RestaurantEnvironment', 'ReviewImage', 'MemberAvatar', 'AvatarFrame')");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarFrames_DeletedBy",
                table: "AvatarFrames",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarFrames_ImageID",
                table: "AvatarFrames",
                column: "ImageID");

            migrationBuilder.CreateIndex(
                name: "IX_MemberAvatarFrames_FrameID",
                table: "MemberAvatarFrames",
                column: "FrameID");

            migrationBuilder.CreateIndex(
                name: "IX_MemberAvatarFrames_MemberID_FrameID",
                table: "MemberAvatarFrames",
                columns: new[] { "MemberID", "FrameID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_CreatedBy",
                table: "PointsTransactions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_MemberID",
                table: "PointsTransactions",
                column: "MemberID");

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_RelatedFrameID",
                table: "PointsTransactions",
                column: "RelatedFrameID");

            migrationBuilder.AddForeignKey(
                name: "FK_Members_AvatarFrames_EquippedFrameID",
                table: "Members",
                column: "EquippedFrameID",
                principalTable: "AvatarFrames",
                principalColumn: "FrameID");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Reports_SourceReportID",
                table: "Notifications",
                column: "SourceReportID",
                principalTable: "Reports",
                principalColumn: "ReportID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Members_AvatarFrames_EquippedFrameID",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Reports_SourceReportID",
                table: "Notifications");

            migrationBuilder.DropTable(
                name: "MemberAvatarFrames");

            migrationBuilder.DropTable(
                name: "PointsTransactions");

            migrationBuilder.DropTable(
                name: "AvatarFrames");

            migrationBuilder.DropIndex(
                name: "IX_Members_EquippedFrameID",
                table: "Members");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Images_ImageType",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "EquippedFrameID",
                table: "Members");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Images_ImageType",
                table: "Images",
                sql: "[ImageType] IN ('RestaurantCover', 'RestaurantEnvironment', 'ReviewImage', 'MemberAvatar')");
        }
    }
}
