using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewWorkflowV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DECIDED_AT_UTC",
                table: "ENROLLMENT",
                type: "TIMESTAMP(7) WITH TIME ZONE",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DECIDED_BY",
                table: "ENROLLMENT",
                type: "VARCHAR2(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DECISION_NOTES",
                table: "ENROLLMENT",
                type: "NVARCHAR2(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ESCALATED_AT_UTC",
                table: "ENROLLMENT",
                type: "TIMESTAMP(7) WITH TIME ZONE",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PROCESS_INSTANCE_KEY",
                table: "ENROLLMENT",
                type: "NUMBER(19)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SCREENING_FLAGS",
                table: "ENROLLMENT",
                type: "VARCHAR2(200)",
                unicode: false,
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NOTIFICATION",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    RECIPIENT = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    KIND = table.Column<string>(type: "VARCHAR2(30)", unicode: false, maxLength: 30, nullable: false),
                    ENROLLMENT_ID = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    REFERENCE_NUMBER = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: true),
                    MESSAGE_EN = table.Column<string>(type: "VARCHAR2(500)", unicode: false, maxLength: 500, nullable: false),
                    MESSAGE_AR = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    CREATED_AT_UTC = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    READ_AT_UTC = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTIFICATION", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_CREATED_AT",
                table: "NOTIFICATION",
                column: "CREATED_AT_UTC");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_RECIPIENT_READ",
                table: "NOTIFICATION",
                columns: new[] { "RECIPIENT", "READ_AT_UTC" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NOTIFICATION");

            migrationBuilder.DropColumn(
                name: "DECIDED_AT_UTC",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "DECIDED_BY",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "DECISION_NOTES",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "ESCALATED_AT_UTC",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "PROCESS_INSTANCE_KEY",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "SCREENING_FLAGS",
                table: "ENROLLMENT");
        }
    }
}
