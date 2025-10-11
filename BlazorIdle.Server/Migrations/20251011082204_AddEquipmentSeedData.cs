using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "affixes",
                columns: new[] { "Id", "allowed_slots_json", "CreatedAt", "ModifierType", "Name", "RarityWeight", "StatType", "UpdatedAt", "ValueMax", "ValueMin" },
                values: new object[,]
                {
                    { "affix_agility", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "敏捷", 1.0, "Agility", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 20.0, 5.0 },
                    { "affix_armor", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "护甲", 1.0, "Armor", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 100.0, 20.0 },
                    { "affix_attack_power", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "攻击强度", 1.0, "AttackPower", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 50.0, 10.0 },
                    { "affix_crit_chance", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Percent", "暴击率", 0.69999999999999996, "CritChance", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 0.050000000000000003, 0.01 },
                    { "affix_dodge", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "闪避等级", 0.90000000000000002, "DodgeRating", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 30.0, 5.0 },
                    { "affix_haste", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Percent", "急速", 0.80000000000000004, "Haste", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 0.10000000000000001, 0.02 },
                    { "affix_health", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "生命值", 1.0, "Health", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 200.0, 50.0 },
                    { "affix_intellect", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "智力", 1.0, "Intellect", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 20.0, 5.0 },
                    { "affix_mastery", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "精通等级", 0.80000000000000004, "MasteryRating", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 50.0, 10.0 },
                    { "affix_spell_power", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "法术强度", 0.69999999999999996, "SpellPower", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 50.0, 10.0 },
                    { "affix_stamina", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "耐力", 1.0, "Stamina", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 20.0, 5.0 },
                    { "affix_strength", null, new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Flat", "力量", 1.0, "Strength", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), 20.0, 5.0 }
                });

            migrationBuilder.InsertData(
                table: "gear_definitions",
                columns: new[] { "Id", "allowed_affix_pool_json", "ArmorType", "base_stats_json", "CreatedAt", "Icon", "Name", "rarity_weights_json", "RequiredLevel", "SetId", "Slot", "tier_multipliers_json", "UpdatedAt", "WeaponType" },
                values: new object[,]
                {
                    { "belt_leather", "[\"affix_agility\",\"affix_stamina\",\"affix_armor\"]", "Leather", "{\"Armor\":{\"Min\":15,\"Max\":25},\"Agility\":{\"Min\":3,\"Max\":8}}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "⚖️", "皮革腰带", "{\"Common\":50,\"Rare\":30,\"Epic\":15,\"Legendary\":5}", 1, null, "Waist", "{\"1\":0.8,\"2\":1,\"3\":1.2}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "None" },
                    { "chest_plate_basic", "[\"affix_strength\",\"affix_stamina\",\"affix_armor\",\"affix_health\"]", "Plate", "{\"Armor\":{\"Min\":40,\"Max\":60},\"Strength\":{\"Min\":5,\"Max\":15}}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "🧥", "板甲胸甲", "{\"Common\":50,\"Rare\":30,\"Epic\":15,\"Legendary\":5}", 1, "set_warrior_basic", "Chest", "{\"1\":0.8,\"2\":1,\"3\":1.2}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "None" },
                    { "helm_cloth_basic", "[\"affix_intellect\",\"affix_stamina\",\"affix_armor\"]", "Cloth", "{\"Armor\":{\"Min\":5,\"Max\":10},\"Intellect\":{\"Min\":3,\"Max\":8}}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "🪖", "布甲头盔", "{\"Common\":50,\"Rare\":30,\"Epic\":15,\"Legendary\":5}", 1, "set_mage_basic", "Head", "{\"1\":0.8,\"2\":1,\"3\":1.2}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "None" },
                    { "ring_basic", "[\"affix_attack_power\",\"affix_crit_chance\",\"affix_haste\"]", "None", "{\"AttackPower\":{\"Min\":5,\"Max\":10}}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "💍", "普通戒指", "{\"Common\":40,\"Rare\":35,\"Epic\":20,\"Legendary\":5}", 1, null, "Finger1", "{\"1\":0.8,\"2\":1,\"3\":1.2}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "None" },
                    { "shield_iron", "[\"affix_armor\",\"affix_stamina\",\"affix_health\"]", "None", "{\"Armor\":{\"Min\":30,\"Max\":50},\"Stamina\":{\"Min\":5,\"Max\":10}}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "🛡️", "铁盾", "{\"Common\":50,\"Rare\":30,\"Epic\":15,\"Legendary\":5}", 1, null, "OffHand", "{\"1\":0.8,\"2\":1,\"3\":1.2}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Shield" },
                    { "weapon_iron_sword", "[\"affix_attack_power\",\"affix_crit_chance\",\"affix_strength\",\"affix_agility\"]", "None", "{\"AttackPower\":{\"Min\":10,\"Max\":15}}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "⚔️", "铁剑", "{\"Common\":50,\"Rare\":30,\"Epic\":15,\"Legendary\":5}", 1, null, "MainHand", "{\"1\":0.8,\"2\":1,\"3\":1.2}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "Sword" }
                });

            migrationBuilder.InsertData(
                table: "gear_sets",
                columns: new[] { "Id", "bonuses_json", "CreatedAt", "Name", "pieces_json", "UpdatedAt" },
                values: new object[,]
                {
                    { "set_mage_basic", "{\"2\":[{\"StatType\":2,\"ModifierType\":0,\"Value\":10}],\"4\":[{\"StatType\":4,\"ModifierType\":0,\"Value\":25},{\"StatType\":15,\"ModifierType\":1,\"Value\":0.03}]}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "法师基础套装", "[\"helm_cloth_basic\"]", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { "set_warrior_basic", "{\"2\":[{\"StatType\":0,\"ModifierType\":0,\"Value\":10}],\"4\":[{\"StatType\":4,\"ModifierType\":0,\"Value\":30},{\"StatType\":17,\"ModifierType\":0,\"Value\":100}]}", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc), "战士基础套装", "[\"chest_plate_basic\"]", new DateTime(2025, 10, 11, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_agility");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_armor");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_attack_power");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_crit_chance");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_dodge");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_haste");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_health");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_intellect");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_mastery");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_spell_power");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_stamina");

            migrationBuilder.DeleteData(
                table: "affixes",
                keyColumn: "Id",
                keyValue: "affix_strength");

            migrationBuilder.DeleteData(
                table: "gear_definitions",
                keyColumn: "Id",
                keyValue: "belt_leather");

            migrationBuilder.DeleteData(
                table: "gear_definitions",
                keyColumn: "Id",
                keyValue: "chest_plate_basic");

            migrationBuilder.DeleteData(
                table: "gear_definitions",
                keyColumn: "Id",
                keyValue: "helm_cloth_basic");

            migrationBuilder.DeleteData(
                table: "gear_definitions",
                keyColumn: "Id",
                keyValue: "ring_basic");

            migrationBuilder.DeleteData(
                table: "gear_definitions",
                keyColumn: "Id",
                keyValue: "shield_iron");

            migrationBuilder.DeleteData(
                table: "gear_definitions",
                keyColumn: "Id",
                keyValue: "weapon_iron_sword");

            migrationBuilder.DeleteData(
                table: "gear_sets",
                keyColumn: "Id",
                keyValue: "set_mage_basic");

            migrationBuilder.DeleteData(
                table: "gear_sets",
                keyColumn: "Id",
                keyValue: "set_warrior_basic");
        }
    }
}
