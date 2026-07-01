using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PastPort.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SetDefaultGoalForVrGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Goal",
                table: "VrSessions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Educational",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Goal",
                table: "SceneCaches",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Educational",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.Sql("UPDATE VrSessions SET Goal = 'Educational' WHERE Goal IS NULL OR LTRIM(RTRIM(Goal)) = ''");
            migrationBuilder.Sql("UPDATE SceneCaches SET Goal = 'Educational' WHERE Goal IS NULL OR LTRIM(RTRIM(Goal)) = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Goal",
                table: "VrSessions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Educational");

            migrationBuilder.AlterColumn<string>(
                name: "Goal",
                table: "SceneCaches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "Educational");
        }
    }
}
