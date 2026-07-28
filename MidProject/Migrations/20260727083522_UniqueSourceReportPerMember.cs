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

            // 先清除既有重複：上一個 migration 移除 unique 後的空窗期可能已寫入重複的
            // (SourceReportID, SourceReportOutcome, MemberID)。若不先去重，下方 unique index 會建立失敗。
            // 同一（檢舉＋結果＋收件人）本應只有一筆通知，故每組保留最小 NotificationID、刪除其餘。
            migrationBuilder.Sql(@"
WITH dup AS (
    SELECT NotificationID,
           ROW_NUMBER() OVER (
               PARTITION BY SourceReportID, SourceReportOutcome, MemberID
               ORDER BY NotificationID) AS rn
    FROM Notifications
    WHERE SourceReportID IS NOT NULL AND SourceReportOutcome IS NOT NULL
)
DELETE FROM Notifications WHERE NotificationID IN (SELECT NotificationID FROM dup WHERE rn > 1);
");

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
