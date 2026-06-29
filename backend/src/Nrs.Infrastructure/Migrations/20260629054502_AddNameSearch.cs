using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNameSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NAME_SEARCH",
                table: "PERSON",
                type: "TEXT",
                maxLength: 420,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PERSON_NAME_SEARCH",
                table: "PERSON",
                column: "NAME_SEARCH");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PERSON_NAME_SEARCH",
                table: "PERSON");

            migrationBuilder.DropColumn(
                name: "NAME_SEARCH",
                table: "PERSON");
        }
    }
}
