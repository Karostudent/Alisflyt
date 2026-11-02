using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alisflyt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHprNumberToIdentityUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HprNumber",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_HprNumber",
                table: "AspNetUsers",
                column: "HprNumber",
                unique: true,
                filter: "[HprNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_HprNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HprNumber",
                table: "AspNetUsers");
        }
    }
}
