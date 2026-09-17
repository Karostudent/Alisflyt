using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alisflyt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GrantCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HprNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmploymentPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    EmploymentStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmploymentEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrantCases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GrantCases_CaseNumber",
                table: "GrantCases",
                column: "CaseNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrantCases");
        }
    }
}
