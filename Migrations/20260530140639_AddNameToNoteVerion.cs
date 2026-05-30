using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteVault.Migrations
{
    /// <inheritdoc />
    public partial class AddNameToNoteVerion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "NoteVersions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "NoteVersions");
        }
    }
}
