using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MidProject.Migrations
{
    /// <inheritdoc />
    public partial class NotificationModuleV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [Notifications]
                    WHERE [CreatedBy] IS NULL
                       OR LEN([Content]) > 1000
                       OR ([NotificationType] = 'Personal' AND ([MemberID] IS NULL OR [TargetRole] IS NOT NULL OR [TargetStatus] IS NOT NULL OR [TargetLevelID] IS NOT NULL))
                       OR ([NotificationType] = 'Condition' AND [MemberID] IS NOT NULL)
                       OR ([IsSent] = 0 AND [SentAt] IS NOT NULL)
                       OR ([IsSent] = 1 AND [SentAt] IS NULL)
                       OR ([IsDeleted] = 0 AND ([DeletedAt] IS NOT NULL OR [DeletedBy] IS NOT NULL))
                       OR ([IsDeleted] = 1 AND ([IsSent] = 1 OR [DeletedAt] IS NULL OR [DeletedBy] IS NULL))
                )
                    THROW 51000, 'NotificationModuleV2 preflight failed: remediate invalid notification data before applying this migration.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsDeleted",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsSent",
                table: "Notifications");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "UserLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Notifications",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Notifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Notifications",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "SourceReportID",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceReportOutcome",
                table: "Notifications",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsDeleted_ScheduledAt_NotificationID",
                table: "Notifications",
                columns: new[] { "IsDeleted", "ScheduledAt", "NotificationID" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsSent_IsDeleted_ScheduledAt_NotificationID",
                table: "Notifications",
                columns: new[] { "IsSent", "IsDeleted", "ScheduledAt", "NotificationID" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SourceReportID_SourceReportOutcome",
                table: "Notifications",
                columns: new[] { "SourceReportID", "SourceReportOutcome" },
                unique: true,
                filter: "[SourceReportID] IS NOT NULL AND [SourceReportOutcome] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Audience",
                table: "Notifications",
                sql: "([NotificationType] = 'Personal' AND [MemberID] IS NOT NULL AND [TargetRole] IS NULL AND [TargetStatus] IS NULL AND [TargetLevelID] IS NULL) OR ([NotificationType] = 'Condition' AND [MemberID] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_DeletedState",
                table: "Notifications",
                sql: "([IsDeleted] = 0 AND [DeletedAt] IS NULL AND [DeletedBy] IS NULL) OR ([IsDeleted] = 1 AND [IsSent] = 0 AND [DeletedAt] IS NOT NULL AND [DeletedBy] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_ReportSource",
                table: "Notifications",
                sql: "([SourceReportID] IS NULL AND [SourceReportOutcome] IS NULL) OR ([SourceReportID] IS NOT NULL AND [SourceReportOutcome] IN ('Approved', 'Rejected'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_SentState",
                table: "Notifications",
                sql: "([IsSent] = 0 AND [SentAt] IS NULL) OR ([IsSent] = 1 AND [SentAt] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsDeleted_ScheduledAt_NotificationID",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsSent_IsDeleted_ScheduledAt_NotificationID",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SourceReportID_SourceReportOutcome",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Audience",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_DeletedState",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_ReportSource",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_SentState",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "UserLevels");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SourceReportID",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "SourceReportOutcome",
                table: "Notifications");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedBy",
                table: "Notifications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Notifications",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsDeleted",
                table: "Notifications",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsSent",
                table: "Notifications",
                column: "IsSent");
        }
    }
}
