using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfHostSekai.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterCostume3D : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Costumes3Ds",
                table: "UserCharacter",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Costumes3Ds",
                table: "UserCharacter");
        }
    }
}
