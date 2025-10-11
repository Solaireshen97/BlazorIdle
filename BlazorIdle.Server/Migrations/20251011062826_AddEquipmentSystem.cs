using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "affixes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StatType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ModifierType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ValueMin = table.Column<double>(type: "REAL", nullable: false),
                    ValueMax = table.Column<double>(type: "REAL", nullable: false),
                    RarityWeight = table.Column<double>(type: "REAL", nullable: false),
                    allowed_slots_json = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_affixes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gear_definitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Slot = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ArmorType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    WeaponType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RequiredLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    base_stats_json = table.Column<string>(type: "TEXT", nullable: false),
                    allowed_affix_pool_json = table.Column<string>(type: "TEXT", nullable: false),
                    rarity_weights_json = table.Column<string>(type: "TEXT", nullable: false),
                    SetId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    tier_multipliers_json = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gear_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gear_sets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    pieces_json = table.Column<string>(type: "TEXT", nullable: false),
                    bonuses_json = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gear_sets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "gear_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DefinitionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SlotType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Rarity = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TierLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    rolled_stats_json = table.Column<string>(type: "TEXT", nullable: false),
                    affixes_json = table.Column<string>(type: "TEXT", nullable: false),
                    QualityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    SetId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsEquipped = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBound = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gear_instances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gear_instances_gear_definitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "gear_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_affixes_ModifierType",
                table: "affixes",
                column: "ModifierType");

            migrationBuilder.CreateIndex(
                name: "IX_affixes_StatType",
                table: "affixes",
                column: "StatType");

            migrationBuilder.CreateIndex(
                name: "IX_gear_definitions_RequiredLevel",
                table: "gear_definitions",
                column: "RequiredLevel");

            migrationBuilder.CreateIndex(
                name: "IX_gear_definitions_SetId",
                table: "gear_definitions",
                column: "SetId");

            migrationBuilder.CreateIndex(
                name: "IX_gear_definitions_Slot",
                table: "gear_definitions",
                column: "Slot");

            migrationBuilder.CreateIndex(
                name: "IX_gear_instances_CharacterId",
                table: "gear_instances",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_gear_instances_CharacterId_SlotType",
                table: "gear_instances",
                columns: new[] { "CharacterId", "SlotType" });

            migrationBuilder.CreateIndex(
                name: "IX_gear_instances_DefinitionId",
                table: "gear_instances",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_gear_instances_IsEquipped",
                table: "gear_instances",
                column: "IsEquipped");

            migrationBuilder.CreateIndex(
                name: "IX_gear_instances_Rarity",
                table: "gear_instances",
                column: "Rarity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "affixes");

            migrationBuilder.DropTable(
                name: "gear_instances");

            migrationBuilder.DropTable(
                name: "gear_sets");

            migrationBuilder.DropTable(
                name: "gear_definitions");
        }
    }
}
