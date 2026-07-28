using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MidProject.Data;

#nullable disable

namespace MidProject.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260728090000_AddMemberRowVersion")]
public partial class AddMemberRowVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Members",
            type: "rowversion",
            rowVersion: true,
            nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RowVersion",
            table: "Members");
    }
}
