using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterOfflineFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastOfflineSettledAtUtc",
                table: "Characters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAtUtc",
                table: "Characters",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastOfflineSettledAtUtc",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "LastSeenAtUtc",
                table: "Characters");
        }
    }
}
