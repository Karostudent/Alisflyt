using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alisflyt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductivityEligibilityMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductivityEligibilityMonths",
                table: "GrantRateSets",
                type: "int",
                nullable: false,
                defaultValue: 24);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductivityEligibilityMonths",
                table: "GrantRateSets");
        }
    }
}
