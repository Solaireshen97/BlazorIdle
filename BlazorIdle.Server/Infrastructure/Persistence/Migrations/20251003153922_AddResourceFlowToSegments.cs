using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceFlowToSegments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResourceFlowJson",
                table: "BattleSegments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TagCountersJson",
                table: "BattleSegments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResourceFlowJson",
                table: "BattleSegments");

            migrationBuilder.DropColumn(
                name: "TagCountersJson",
                table: "BattleSegments");
        }
    }
}
