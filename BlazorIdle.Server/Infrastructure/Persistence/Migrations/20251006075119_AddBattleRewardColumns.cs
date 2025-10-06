using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleRewardColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DungeonId",
                table: "Battles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DungeonRuns",
                table: "Battles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Exp",
                table: "Battles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Gold",
                table: "Battles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LootJson",
                table: "Battles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardType",
                table: "Battles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DungeonId",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "DungeonRuns",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "Exp",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "Gold",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "LootJson",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "RewardType",
                table: "Battles");
        }
    }
}
