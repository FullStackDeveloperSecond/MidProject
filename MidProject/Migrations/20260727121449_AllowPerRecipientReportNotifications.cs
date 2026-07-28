using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MidProject.Data;

#nullable disable

namespace MidProject.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260727121449_AllowPerRecipientReportNotifications")]
    public partial class AllowPerRecipientReportNotifications : Migration
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
                filter: "[SourceReportID] IS NOT NULL AND [SourceReportOutcome] IS NOT NULL AND [MemberID] IS NOT NULL");
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
                unique: true,
                filter: "[SourceReportID] IS NOT NULL AND [SourceReportOutcome] IS NOT NULL");
        }
    }
}
