using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace NoteVault.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Notes",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('english', coalesce(\"Title\", '') || ' ' || coalesce(regexp_replace(\"Content\", '<[^>]*>', ' ', 'g'), ''))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_SearchVector",
                table: "Notes",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notes_SearchVector",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Notes");
        }
    }
}
