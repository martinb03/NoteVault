using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteVault.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteCopyMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CopiedFromTitle",
                table: "Notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CopiedFromUserId",
                table: "Notes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_CopiedFromUserId",
                table: "Notes",
                column: "CopiedFromUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_AspNetUsers_CopiedFromUserId",
                table: "Notes",
                column: "CopiedFromUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notes_AspNetUsers_CopiedFromUserId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_CopiedFromUserId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "CopiedFromTitle",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "CopiedFromUserId",
                table: "Notes");
        }
    }
}
