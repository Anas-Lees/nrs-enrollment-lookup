using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUDIT_ENTRY",
                columns: table => new
                {
                    AUDIT_ID = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TIMESTAMP_UTC = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ACTOR = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    ACTION = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    TARGET_CRN = table.Column<string>(type: "TEXT", unicode: false, maxLength: 9, nullable: true),
                    CRITERIA = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    RESULT_COUNT = table.Column<int>(type: "INTEGER", nullable: true),
                    SOURCE_IP = table.Column<string>(type: "TEXT", unicode: false, maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_ENTRY", x => x.AUDIT_ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_ENTRY_ACTOR",
                table: "AUDIT_ENTRY",
                column: "ACTOR");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_ENTRY_TIMESTAMP",
                table: "AUDIT_ENTRY",
                column: "TIMESTAMP_UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AUDIT_ENTRY");
        }
    }
}
