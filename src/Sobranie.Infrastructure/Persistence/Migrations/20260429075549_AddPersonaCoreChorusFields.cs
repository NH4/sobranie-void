using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sobranie.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonaCoreChorusFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Speeches_MPs_MPId",
                table: "Speeches");

            migrationBuilder.AlterColumn<string>(
                name: "MPId",
                table: "Speeches",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddForeignKey(
                name: "FK_Speeches_MPs_MPId",
                table: "Speeches",
                column: "MPId",
                principalTable: "MPs",
                principalColumn: "MPId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Speeches_MPs_MPId",
                table: "Speeches");

            migrationBuilder.AlterColumn<string>(
                name: "MPId",
                table: "Speeches",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Speeches_MPs_MPId",
                table: "Speeches",
                column: "MPId",
                principalTable: "MPs",
                principalColumn: "MPId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
