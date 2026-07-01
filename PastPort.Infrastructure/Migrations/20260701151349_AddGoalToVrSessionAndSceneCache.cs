using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PastPort.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalToVrSessionAndSceneCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Goal",
                table: "VrSessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Educational");

            migrationBuilder.AddColumn<string>(
                name: "Goal",
                table: "SceneCaches",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Educational");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Goal",
                table: "VrSessions");

            migrationBuilder.DropColumn(
                name: "Goal",
                table: "SceneCaches");
        }
    }
}
