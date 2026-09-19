using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alisflyt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGrantCaseReturnWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "GrantCases",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReturnedAtUtc",
                table: "GrantCases",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "GrantCases");

            migrationBuilder.DropColumn(
                name: "ReturnedAtUtc",
                table: "GrantCases");
        }
    }
}
