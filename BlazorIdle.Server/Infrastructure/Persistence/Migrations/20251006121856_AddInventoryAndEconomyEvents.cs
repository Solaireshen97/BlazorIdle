using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndEconomyEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RunningBattleSnapshotRecord",
                table: "RunningBattleSnapshotRecord");

            migrationBuilder.RenameTable(
                name: "RunningBattleSnapshotRecord",
                newName: "RunningBattleSnapshots");

            migrationBuilder.RenameIndex(
                name: "IX_RunningBattleSnapshotRecord_StepBattleId",
                table: "RunningBattleSnapshots",
                newName: "IX_RunningBattleSnapshots_StepBattleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RunningBattleSnapshots",
                table: "RunningBattleSnapshots",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "economy_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BattleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Gold = table.Column<long>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false),
                    ItemsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_economy_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_items_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_economy_events_BattleId",
                table: "economy_events",
                column: "BattleId",
                filter: "BattleId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_economy_events_CharacterId",
                table: "economy_events",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_economy_events_IdempotencyKey",
                table: "economy_events",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_items_CharacterId_ItemId",
                table: "inventory_items",
                columns: new[] { "CharacterId", "ItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "economy_events");

            migrationBuilder.DropTable(
                name: "inventory_items");

            migrationBuilder.DropPrimaryKey(
                name: "PK_RunningBattleSnapshots",
                table: "RunningBattleSnapshots");

            migrationBuilder.RenameTable(
                name: "RunningBattleSnapshots",
                newName: "RunningBattleSnapshotRecord");

            migrationBuilder.RenameIndex(
                name: "IX_RunningBattleSnapshots_StepBattleId",
                table: "RunningBattleSnapshotRecord",
                newName: "IX_RunningBattleSnapshotRecord_StepBattleId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RunningBattleSnapshotRecord",
                table: "RunningBattleSnapshotRecord",
                column: "Id");
        }
    }
}
