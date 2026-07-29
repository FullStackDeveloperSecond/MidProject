using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MidProject.Data;

#nullable disable

namespace MidProject.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260727160000_StrengthenPointsTransactionRules")]
    public partial class StrengthenPointsTransactionRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_PointsTransactions_AmountByType",
                table: "PointsTransactions",
                sql: "([Type] = 'Redeem' AND [Amount] < 0) OR ([Type] = 'Earn' AND [Amount] > 0) OR ([Type] = 'AdminAdjust' AND [Amount] <> 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointsTransactions_CreatedByType",
                table: "PointsTransactions",
                sql: "([Type] = 'AdminAdjust' AND [CreatedBy] IS NOT NULL) OR ([Type] IN ('Redeem', 'Earn') AND [CreatedBy] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PointsTransactions_AmountByType",
                table: "PointsTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PointsTransactions_CreatedByType",
                table: "PointsTransactions");
        }
    }
}
