using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nrs.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ASSIGNED_AT_UTC",
                table: "ENROLLMENT",
                type: "TIMESTAMP(7) WITH TIME ZONE",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ASSIGNED_TO",
                table: "ENROLLMENT",
                type: "VARCHAR2(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            // Reconcile the semantic change of this release. Before ownership, UNDER_REVIEW meant
            // "sitting in the shared review queue" (there was no assignee). That is now spelled
            // PENDING_REVIEW; UNDER_REVIEW now means "claimed by a reviewer". Any pre-existing
            // UNDER_REVIEW row has no assignee, so move it to PENDING_REVIEW — otherwise it would
            // be stuck: not claimable (not pending) and not decidable (no assignee to match).
            migrationBuilder.Sql(
                "UPDATE \"ENROLLMENT\" SET \"STATUS\" = 'PENDING_REVIEW' " +
                "WHERE \"STATUS\" = 'UNDER_REVIEW' AND \"ASSIGNED_TO\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ASSIGNED_AT_UTC",
                table: "ENROLLMENT");

            migrationBuilder.DropColumn(
                name: "ASSIGNED_TO",
                table: "ENROLLMENT");
        }
    }
}
