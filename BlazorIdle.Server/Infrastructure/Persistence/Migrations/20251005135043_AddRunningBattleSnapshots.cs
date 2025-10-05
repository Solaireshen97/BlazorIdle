using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunningBattleSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RunningBattleSnapshotRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepBattleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Profession = table.Column<int>(type: "INTEGER", nullable: false),
                    EnemyId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EnemyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Seed = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TargetSeconds = table.Column<double>(type: "REAL", nullable: false),
                    SimulatedSeconds = table.Column<double>(type: "REAL", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunningBattleSnapshotRecord", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RunningBattleSnapshotRecord_StepBattleId",
                table: "RunningBattleSnapshotRecord",
                column: "StepBattleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunningBattleSnapshotRecord");
        }
    }
}
