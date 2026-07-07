using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CardEnrollmentUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_ID_CARD_ENROLLMENT",
                table: "ID_CARD",
                column: "ENROLLMENT_ID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ID_CARD_ENROLLMENT",
                table: "ID_CARD");
        }
    }
}
