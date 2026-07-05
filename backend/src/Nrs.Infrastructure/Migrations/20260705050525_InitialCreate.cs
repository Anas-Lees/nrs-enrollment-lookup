using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUDIT_ENTRY",
                columns: table => new
                {
                    AUDIT_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TIMESTAMP_UTC = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    ACTOR = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    ACTION = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    TARGET_CRN = table.Column<string>(type: "VARCHAR2(9)", unicode: false, maxLength: 9, nullable: true),
                    CRITERIA = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: true),
                    RESULT_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    SOURCE_IP = table.Column<string>(type: "VARCHAR2(45)", unicode: false, maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_ENTRY", x => x.AUDIT_ID);
                });

            migrationBuilder.CreateTable(
                name: "ENROLLMENT",
                columns: table => new
                {
                    ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    REFERENCE_NUMBER = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    CIVIL_NUMBER = table.Column<string>(type: "VARCHAR2(9)", unicode: false, maxLength: 9, nullable: true),
                    FIRST_NAME_EN = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    FAMILY_NAME_EN = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    FIRST_NAME_AR = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    FAMILY_NAME_AR = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    DATE_OF_BIRTH = table.Column<string>(type: "NVARCHAR2(10)", nullable: false),
                    NATIONALITY_CODE = table.Column<string>(type: "VARCHAR2(3)", unicode: false, maxLength: 3, nullable: false),
                    TYPE = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    STATUS = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CREATED_BY = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    CREATED_AT_UTC = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    UPDATED_AT_UTC = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ENROLLMENT", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "NATIONALITY",
                columns: table => new
                {
                    CODE = table.Column<string>(type: "VARCHAR2(3)", unicode: false, maxLength: 3, nullable: false),
                    NAME_EN = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    NAME_AR = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NATIONALITY", x => x.CODE);
                });

            migrationBuilder.CreateTable(
                name: "PERSON",
                columns: table => new
                {
                    CIVIL_NUMBER = table.Column<string>(type: "VARCHAR2(9)", unicode: false, maxLength: 9, nullable: false),
                    FIRST_NAME_AR = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    FAMILY_NAME_AR = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    FIRST_NAME_EN = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    FAMILY_NAME_EN = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: false),
                    NAME_SEARCH = table.Column<string>(type: "NVARCHAR2(420)", maxLength: 420, nullable: true),
                    DATE_OF_BIRTH = table.Column<string>(type: "NVARCHAR2(10)", nullable: false),
                    GENDER = table.Column<string>(type: "CHAR(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: false),
                    NATIONALITY_CODE = table.Column<string>(type: "VARCHAR2(3)", unicode: false, maxLength: 3, nullable: false),
                    STATUS = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    PHOTO_PATH = table.Column<string>(type: "VARCHAR2(500)", unicode: false, maxLength: 500, nullable: true),
                    PLACE_OF_BIRTH_EN = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: true),
                    PLACE_OF_BIRTH_AR = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    MOTHER_NAME_EN = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: true),
                    MOTHER_NAME_AR = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true),
                    MARITAL_STATUS = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: true),
                    BLOOD_TYPE = table.Column<string>(type: "VARCHAR2(3)", unicode: false, maxLength: 3, nullable: true),
                    OCCUPATION_EN = table.Column<string>(type: "VARCHAR2(100)", unicode: false, maxLength: 100, nullable: true),
                    OCCUPATION_AR = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PERSON", x => x.CIVIL_NUMBER);
                    table.ForeignKey(
                        name: "FK_PERSON_NATIONALITY_NATIONALITY_CODE",
                        column: x => x.NATIONALITY_CODE,
                        principalTable: "NATIONALITY",
                        principalColumn: "CODE",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ADDRESS",
                columns: table => new
                {
                    CIVIL_NUMBER = table.Column<string>(type: "VARCHAR2(9)", unicode: false, maxLength: 9, nullable: false),
                    GOVERNORATE = table.Column<string>(type: "VARCHAR2(50)", unicode: false, maxLength: 50, nullable: false),
                    WILAYAT = table.Column<string>(type: "VARCHAR2(50)", unicode: false, maxLength: 50, nullable: false),
                    VILLAGE = table.Column<string>(type: "VARCHAR2(80)", unicode: false, maxLength: 80, nullable: true),
                    STREET = table.Column<string>(type: "VARCHAR2(120)", unicode: false, maxLength: 120, nullable: true),
                    BUILDING_NO = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: true),
                    POSTAL_CODE = table.Column<string>(type: "VARCHAR2(10)", unicode: false, maxLength: 10, nullable: true)
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
                    CIVIL_NUMBER = table.Column<string>(type: "VARCHAR2(9)", unicode: false, maxLength: 9, nullable: false),
                    MOBILE = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: true),
                    EMAIL = table.Column<string>(type: "VARCHAR2(120)", unicode: false, maxLength: 120, nullable: true)
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
                name: "ID_CARD",
                columns: table => new
                {
                    ID_CARD_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CIVIL_NUMBER = table.Column<string>(type: "VARCHAR2(9)", unicode: false, maxLength: 9, nullable: false),
                    CARD_NUMBER = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    ISSUE_DATE = table.Column<string>(type: "NVARCHAR2(10)", nullable: true),
                    EXPIRY_DATE = table.Column<string>(type: "NVARCHAR2(10)", nullable: true),
                    STATUS = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    CARD_TYPE = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false)
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
                    PASSPORT_ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    CIVIL_NUMBER = table.Column<string>(type: "VARCHAR2(9)", unicode: false, maxLength: 9, nullable: false),
                    PASSPORT_NUMBER = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    PASSPORT_TYPE = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false),
                    ISSUE_DATE = table.Column<string>(type: "NVARCHAR2(10)", nullable: true),
                    EXPIRY_DATE = table.Column<string>(type: "NVARCHAR2(10)", nullable: true),
                    STATUS = table.Column<string>(type: "VARCHAR2(20)", unicode: false, maxLength: 20, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_ENTRY_ACTOR",
                table: "AUDIT_ENTRY",
                column: "ACTOR");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_ENTRY_TIMESTAMP",
                table: "AUDIT_ENTRY",
                column: "TIMESTAMP_UTC");

            migrationBuilder.CreateIndex(
                name: "IX_ENROLLMENT_CIVIL_NUMBER",
                table: "ENROLLMENT",
                column: "CIVIL_NUMBER");

            migrationBuilder.CreateIndex(
                name: "IX_ENROLLMENT_CREATED_AT",
                table: "ENROLLMENT",
                column: "CREATED_AT_UTC");

            migrationBuilder.CreateIndex(
                name: "IX_ENROLLMENT_REFERENCE_NUMBER",
                table: "ENROLLMENT",
                column: "REFERENCE_NUMBER",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ENROLLMENT_STATUS",
                table: "ENROLLMENT",
                column: "STATUS");

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
                name: "IX_PERSON_NAME_SEARCH",
                table: "PERSON",
                column: "NAME_SEARCH");

            migrationBuilder.CreateIndex(
                name: "IX_PERSON_NATIONALITY_CODE",
                table: "PERSON",
                column: "NATIONALITY_CODE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADDRESS");

            migrationBuilder.DropTable(
                name: "AUDIT_ENTRY");

            migrationBuilder.DropTable(
                name: "CONTACT");

            migrationBuilder.DropTable(
                name: "ENROLLMENT");

            migrationBuilder.DropTable(
                name: "ID_CARD");

            migrationBuilder.DropTable(
                name: "PASSPORT");

            migrationBuilder.DropTable(
                name: "PERSON");

            migrationBuilder.DropTable(
                name: "NATIONALITY");
        }
    }
}
