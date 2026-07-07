using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrollmentFullRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BLOOD_TYPE",
                table: "ENROLLMENT",
                type: "VARCHAR2(3)",
                unicode: false,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BUILDING_NO",
                table: "ENROLLMENT",
                type: "VARCHAR2(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EMAIL",
                table: "ENROLLMENT",
                type: "VARCHAR2(120)",
                unicode: false,
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GOVERNORATE",
                table: "ENROLLMENT",
                type: "VARCHAR2(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MARITAL_STATUS",
                table: "ENROLLMENT",
                type: "VARCHAR2(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MOBILE",
                table: "ENROLLMENT",
                type: "VARCHAR2(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MOTHER_NAME_AR",
                table: "ENROLLMENT",
                type: "NVARCHAR2(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MOTHER_NAME_EN",
                table: "ENROLLMENT",
                type: "VARCHAR2(150)",
                unicode: false,
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OCCUPATION_AR",
                table: "ENROLLMENT",
                type: "NVARCHAR2(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OCCUPATION_EN",
                table: "ENROLLMENT",
                type: "VARCHAR2(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PASSPORT_EXPIRY_DATE",
                table: "ENROLLMENT",
                type: "NVARCHAR2(10)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PASSPORT_ISSUE_DATE",
                table: "ENROLLMENT",
                type: "NVARCHAR2(10)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PASSPORT_NUMBER",
                table: "ENROLLMENT",
                type: "VARCHAR2(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PASSPORT_TYPE",
                table: "ENROLLMENT",
                type: "VARCHAR2(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PLACE_OF_BIRTH_AR",
                table: "ENROLLMENT",
                type: "NVARCHAR2(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PLACE_OF_BIRTH_EN",
                table: "ENROLLMENT",
                type: "VARCHAR2(80)",
                unicode: false,
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "POSTAL_CODE",
                table: "ENROLLMENT",
                type: "VARCHAR2(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "STREET",
                table: "ENROLLMENT",
                type: "VARCHAR2(120)",
                unicode: false,
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VILLAGE",
                table: "ENROLLMENT",
                type: "VARCHAR2(80)",
                unicode: false,
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WILAYAT",
                table: "ENROLLMENT",
                type: "VARCHAR2(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BLOOD_TYPE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "BUILDING_NO",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "EMAIL",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "GOVERNORATE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "MARITAL_STATUS",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "MOBILE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "MOTHER_NAME_AR",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "MOTHER_NAME_EN",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "OCCUPATION_AR",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "OCCUPATION_EN",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "PASSPORT_EXPIRY_DATE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "PASSPORT_ISSUE_DATE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "PASSPORT_NUMBER",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "PASSPORT_TYPE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "PLACE_OF_BIRTH_AR",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "PLACE_OF_BIRTH_EN",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "POSTAL_CODE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "STREET",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "VILLAGE",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "WILAYAT",
                table: "ENROLLMENT");
        }
    }
}
