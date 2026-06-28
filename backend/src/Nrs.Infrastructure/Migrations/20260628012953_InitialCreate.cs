using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PERSON",
                columns: table => new
                {
                    CIVIL_NUMBER = table.Column<string>(type: "TEXT", unicode: false, maxLength: 9, nullable: false),
                    FIRST_NAME_AR = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FAMILY_NAME_AR = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FIRST_NAME_EN = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    FAMILY_NAME_EN = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    DATE_OF_BIRTH = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    GENDER = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 1, nullable: false),
                    NATIONALITY_CODE = table.Column<string>(type: "TEXT", unicode: false, maxLength: 3, nullable: false),
                    STATUS = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    PHOTO_PATH = table.Column<string>(type: "TEXT", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERSON", x => x.CIVIL_NUMBER);
                });

            migrationBuilder.CreateTable(
                name: "ID_CARD",
                columns: table => new
                {
                    ID_CARD_ID = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CIVIL_NUMBER = table.Column<string>(type: "TEXT", unicode: false, maxLength: 9, nullable: false),
                    CARD_NUMBER = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    ISSUE_DATE = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EXPIRY_DATE = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    STATUS = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    CARD_TYPE = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ID_CARD", x => x.ID_CARD_ID);
                    table.ForeignKey(
                        name: "FK_ID_CARD_PERSON_CIVIL_NUMBER",
                        column: x => x.CIVIL_NUMBER,
                        principalTable: "PERSON",
                        principalColumn: "CIVIL_NUMBER",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PASSPORT",
                columns: table => new
                {
                    PASSPORT_ID = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CIVIL_NUMBER = table.Column<string>(type: "TEXT", unicode: false, maxLength: 9, nullable: false),
                    PASSPORT_NUMBER = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    PASSPORT_TYPE = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false),
                    ISSUE_DATE = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    EXPIRY_DATE = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    STATUS = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PASSPORT", x => x.PASSPORT_ID);
                    table.ForeignKey(
                        name: "FK_PASSPORT_PERSON_CIVIL_NUMBER",
                        column: x => x.CIVIL_NUMBER,
                        principalTable: "PERSON",
                        principalColumn: "CIVIL_NUMBER",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ID_CARD_CIVIL_NUMBER",
                table: "ID_CARD",
                column: "CIVIL_NUMBER");

            migrationBuilder.CreateIndex(
                name: "IX_PASSPORT_CIVIL_NUMBER",
                table: "PASSPORT",
                column: "CIVIL_NUMBER");

            migrationBuilder.CreateIndex(
                name: "IX_PERSON_DATE_OF_BIRTH",
                table: "PERSON",
                column: "DATE_OF_BIRTH");

            migrationBuilder.CreateIndex(
                name: "IX_PERSON_FAMILY_NAME_AR",
                table: "PERSON",
                column: "FAMILY_NAME_AR");

            migrationBuilder.CreateIndex(
                name: "IX_PERSON_FAMILY_NAME_EN",
                table: "PERSON",
                column: "FAMILY_NAME_EN");

            migrationBuilder.CreateIndex(
                name: "IX_PERSON_NATIONALITY_CODE",
                table: "PERSON",
                column: "NATIONALITY_CODE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ID_CARD");

            migrationBuilder.DropTable(
                name: "PASSPORT");

            migrationBuilder.DropTable(
                name: "PERSON");
        }
    }
}
