using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitGameSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Battles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Battles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BattleSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BattleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTime = table.Column<double>(type: "REAL", nullable: false),
                    EndTime = table.Column<double>(type: "REAL", nullable: false),
                    EventCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    DamageBySourceJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattleSegments_Battles_BattleId",
                        column: x => x.BattleId,
                        principalTable: "Battles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Battles_CharacterId",
                table: "Battles",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleSegments_BattleId",
                table: "BattleSegments",
                column: "BattleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleSegments");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "GameData");

            migrationBuilder.DropTable(
                name: "Battles");
        }
    }
}
