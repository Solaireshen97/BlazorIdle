using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEnemyColumnsToBattles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DamageByTypeJson",
                table: "BattleSegments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "EnemyArmor",
                table: "Battles",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "EnemyId",
                table: "Battles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EnemyLevel",
                table: "Battles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "EnemyMagicResist",
                table: "Battles",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "EnemyMaxHp",
                table: "Battles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EnemyName",
                table: "Battles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "KillTimeSeconds",
                table: "Battles",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Killed",
                table: "Battles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OverkillDamage",
                table: "Battles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DamageByTypeJson",
                table: "BattleSegments");

            migrationBuilder.DropColumn(
                name: "EnemyArmor",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "EnemyId",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "EnemyLevel",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "EnemyMagicResist",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "EnemyMaxHp",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "EnemyName",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "KillTimeSeconds",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "Killed",
                table: "Battles");

            migrationBuilder.DropColumn(
                name: "OverkillDamage",
                table: "Battles");
        }
    }
}
