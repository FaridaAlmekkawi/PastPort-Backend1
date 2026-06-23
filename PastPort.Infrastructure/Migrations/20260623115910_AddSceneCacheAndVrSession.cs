using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PastPort.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSceneCacheAndVrSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_FileName",
                table: "Assets");

            migrationBuilder.AlterColumn<string>(
                name: "SourcePromptHash",
                table: "Assets",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateTable(
                name: "SceneCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CacheKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Civilization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YearRange = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationOldName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleOrName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SceneJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SceneCaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VrSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Civilization = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    YearRange = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationOldName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoleOrName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VrSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_SourcePromptHash",
                table: "Assets",
                column: "SourcePromptHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SceneCaches");

            migrationBuilder.DropTable(
                name: "VrSessions");

            migrationBuilder.DropIndex(
                name: "IX_Assets_SourcePromptHash",
                table: "Assets");

            migrationBuilder.AlterColumn<string>(
                name: "SourcePromptHash",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "Assets",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_FileName",
                table: "Assets",
                column: "FileName");
        }
    }
}
