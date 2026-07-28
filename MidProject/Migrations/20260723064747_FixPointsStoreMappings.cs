using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidProject.Migrations
{
    /// <inheritdoc />
    public partial class FixPointsStoreMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PointsTransactions_MemberID",
                table: "PointsTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_MemberID_CreatedAt",
                table: "PointsTransactions",
                columns: new[] { "MemberID", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_Type",
                table: "PointsTransactions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarFrames_IsActive",
                table: "AvatarFrames",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AvatarFrames_IsDeleted",
                table: "AvatarFrames",
                column: "IsDeleted");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AvatarFrames_PointsPrice",
                table: "AvatarFrames",
                sql: "[PointsPrice] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PointsTransactions_MemberID_CreatedAt",
                table: "PointsTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointsTransactions_Type",
                table: "PointsTransactions");

            migrationBuilder.DropIndex(
                name: "IX_AvatarFrames_IsActive",
                table: "AvatarFrames");

            migrationBuilder.DropIndex(
                name: "IX_AvatarFrames_IsDeleted",
                table: "AvatarFrames");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AvatarFrames_PointsPrice",
                table: "AvatarFrames");

            migrationBuilder.CreateIndex(
                name: "IX_PointsTransactions_MemberID",
                table: "PointsTransactions",
                column: "MemberID");
        }
    }
}
