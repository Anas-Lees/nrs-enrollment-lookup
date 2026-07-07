using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CardFulfilment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ENROLLMENT_ID",
                table: "ID_CARD",
                type: "RAW(16)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ID_CARD_STATUS",
                table: "ID_CARD",
                column: "STATUS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ID_CARD_STATUS",
                table: "ID_CARD");

            migrationBuilder.DropColumn(
                name: "ENROLLMENT_ID",
                table: "ID_CARD");
        }
    }
}
