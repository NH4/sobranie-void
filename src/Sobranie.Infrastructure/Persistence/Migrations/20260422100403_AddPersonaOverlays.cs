using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sobranie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonaOverlays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PersonaSystemPrompt",
                table: "MPs",
                newName: "PersonaCore");

            migrationBuilder.AddColumn<string>(
                name: "PersonaOverlayAbsurd",
                table: "MPs",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonaOverlayGentle",
                table: "MPs",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonaOverlaySharp",
                table: "MPs",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PersonaOverlayAbsurd",
                table: "MPs");

            migrationBuilder.DropColumn(
                name: "PersonaOverlayGentle",
                table: "MPs");

            migrationBuilder.DropColumn(
                name: "PersonaOverlaySharp",
                table: "MPs");

            migrationBuilder.RenameColumn(
                name: "PersonaCore",
                table: "MPs",
                newName: "PersonaSystemPrompt");
        }
    }
}
