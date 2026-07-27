using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidProject.Migrations
{
    /// <inheritdoc />
    public partial class UniqueSourceReportPerMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_SourceReportID_SourceReportOutcome",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SourceReportID_SourceReportOutcome_MemberID",
                table: "Notifications",
                columns: new[] { "SourceReportID", "SourceReportOutcome", "MemberID" },
                unique: true,
                filter: "[SourceReportID] IS NOT NULL AND [SourceReportOutcome] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_SourceReportID_SourceReportOutcome_MemberID",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SourceReportID_SourceReportOutcome",
                table: "Notifications",
                columns: new[] { "SourceReportID", "SourceReportOutcome" },
                filter: "[SourceReportID] IS NOT NULL AND [SourceReportOutcome] IS NOT NULL");
        }
    }
}
