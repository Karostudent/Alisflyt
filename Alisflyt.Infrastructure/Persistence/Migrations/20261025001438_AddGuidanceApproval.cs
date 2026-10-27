using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alisflyt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidanceApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuidanceApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DoctorHprNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupervisorHprNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupervisorUserKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SupervisorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestedRevisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    AttestationSnapshotHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AttestationSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidanceApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuidanceApprovals_GrantCases_GrantCaseId",
                        column: x => x.GrantCaseId,
                        principalTable: "GrantCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceApprovals_GrantCaseId",
                table: "GuidanceApprovals",
                column: "GrantCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceApprovals_GrantCaseId_VersionNumber",
                table: "GuidanceApprovals",
                columns: new[] { "GrantCaseId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_GuidanceApprovals_Status",
                table: "GuidanceApprovals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuidanceApprovals");
        }
    }
}
