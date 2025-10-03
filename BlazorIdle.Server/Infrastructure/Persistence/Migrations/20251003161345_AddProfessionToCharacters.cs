using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorIdle.Server.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionToCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Profession",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Profession",
                table: "Characters");
        }
    }
}
