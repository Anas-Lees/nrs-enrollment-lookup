using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewLifecycleV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CORRECTION_NOTE",
                table: "ENROLLMENT",
                type: "NVARCHAR2(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GENDER",
                table: "ENROLLMENT",
                type: "VARCHAR2(1)",
                unicode: false,
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RISK_LEVEL",
                table: "ENROLLMENT",
                type: "VARCHAR2(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CORRECTION_NOTE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "GENDER",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "RISK_LEVEL",
                table: "ENROLLMENT");
        }
    }
}
