using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PastPort.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePromptHashToAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourcePromptHash",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourcePromptHash",
                table: "Assets");
        }
    }
}
