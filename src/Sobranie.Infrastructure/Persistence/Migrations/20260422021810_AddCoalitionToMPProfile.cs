using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sobranie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoalitionToMPProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Coalition",
                table: "MPs",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Coalition",
                table: "MPs");
        }
    }
}
