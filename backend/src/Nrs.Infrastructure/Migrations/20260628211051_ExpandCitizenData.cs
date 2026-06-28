using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Generated seed data — constant array arguments are fine here

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCitizenData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BLOOD_TYPE",
                table: "PERSON",
                type: "TEXT",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MARITAL_STATUS",
                table: "PERSON",
                type: "TEXT",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MOTHER_NAME_AR",
                table: "PERSON",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MOTHER_NAME_EN",
                table: "PERSON",
                type: "TEXT",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OCCUPATION_AR",
                table: "PERSON",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OCCUPATION_EN",
                table: "PERSON",
                type: "TEXT",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PLACE_OF_BIRTH_AR",
                table: "PERSON",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PLACE_OF_BIRTH_EN",
                table: "PERSON",
                type: "TEXT",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ADDRESS",
                columns: table => new
                {
                    CIVIL_NUMBER = table.Column<string>(type: "TEXT", unicode: false, maxLength: 9, nullable: false),
                    GOVERNORATE = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    WILAYAT = table.Column<string>(type: "TEXT", unicode: false, maxLength: 50, nullable: false),
                    VILLAGE = table.Column<string>(type: "TEXT", unicode: false, maxLength: 80, nullable: true),
                    STREET = table.Column<string>(type: "TEXT", unicode: false, maxLength: 120, nullable: true),
                    BUILDING_NO = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: true),
                    POSTAL_CODE = table.Column<string>(type: "TEXT", unicode: false, maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADDRESS", x => x.CIVIL_NUMBER);
                    table.ForeignKey(
                        name: "FK_ADDRESS_PERSON_CIVIL_NUMBER",
                        column: x => x.CIVIL_NUMBER,
                        principalTable: "PERSON",
                        principalColumn: "CIVIL_NUMBER",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CONTACT",
                columns: table => new
                {
                    CIVIL_NUMBER = table.Column<string>(type: "TEXT", unicode: false, maxLength: 9, nullable: false),
                    MOBILE = table.Column<string>(type: "TEXT", unicode: false, maxLength: 20, nullable: true),
                    EMAIL = table.Column<string>(type: "TEXT", unicode: false, maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTACT", x => x.CIVIL_NUMBER);
                    table.ForeignKey(
                        name: "FK_CONTACT_PERSON_CIVIL_NUMBER",
                        column: x => x.CIVIL_NUMBER,
                        principalTable: "PERSON",
                        principalColumn: "CIVIL_NUMBER",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NATIONALITY",
                columns: table => new
                {
                    CODE = table.Column<string>(type: "TEXT", unicode: false, maxLength: 3, nullable: false),
                    NAME_EN = table.Column<string>(type: "TEXT", unicode: false, maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NATIONALITY", x => x.CODE);
                });

            migrationBuilder.InsertData(
                table: "NATIONALITY",
                columns: new[] { "CODE", "NAME_AR", "NAME_EN" },
                values: new object[,]
                {
                    { "ARE", "الإمارات", "United Arab Emirates" },
                    { "BGD", "بنغلاديش", "Bangladesh" },
                    { "BHR", "البحرين", "Bahrain" },
                    { "EGY", "مصر", "Egypt" },
                    { "GBR", "المملكة المتحدة", "United Kingdom" },
                    { "IND", "الهند", "India" },
                    { "JOR", "الأردن", "Jordan" },
                    { "KWT", "الكويت", "Kuwait" },
                    { "LKA", "سريلانكا", "Sri Lanka" },
                    { "OMN", "عُمان", "Oman" },
                    { "PAK", "باكستان", "Pakistan" },
                    { "PHL", "الفلبين", "Philippines" },
                    { "QAT", "قطر", "Qatar" },
                    { "SAU", "السعودية", "Saudi Arabia" },
                    { "USA", "الولايات المتحدة", "United States" },
                    { "YEM", "اليمن", "Yemen" }
                });

            migrationBuilder.AddForeignKey(
                name: "FK_PERSON_NATIONALITY_NATIONALITY_CODE",
                table: "PERSON",
                column: "NATIONALITY_CODE",
                principalTable: "NATIONALITY",
                principalColumn: "CODE",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PERSON_NATIONALITY_NATIONALITY_CODE",
                table: "PERSON");

            migrationBuilder.DropTable(
                name: "ADDRESS");

            migrationBuilder.DropTable(
                name: "CONTACT");

            migrationBuilder.DropTable(
                name: "NATIONALITY");

            migrationBuilder.DropColumn(
                name: "BLOOD_TYPE",
                table: "PERSON");

            migrationBuilder.DropColumn(
                name: "MARITAL_STATUS",
                table: "PERSON");

            migrationBuilder.DropColumn(
                name: "MOTHER_NAME_AR",
                table: "PERSON");

            migrationBuilder.DropColumn(
                name: "MOTHER_NAME_EN",
                table: "PERSON");

            migrationBuilder.DropColumn(
                name: "OCCUPATION_AR",
                table: "PERSON");

            migrationBuilder.DropColumn(
                name: "OCCUPATION_EN",
                table: "PERSON");

            migrationBuilder.DropColumn(
                name: "PLACE_OF_BIRTH_AR",
                table: "PERSON");

            migrationBuilder.DropColumn(
                name: "PLACE_OF_BIRTH_EN",
                table: "PERSON");
        }
    }
}
