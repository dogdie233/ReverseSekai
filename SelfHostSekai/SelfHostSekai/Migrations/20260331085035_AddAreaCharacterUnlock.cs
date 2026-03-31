using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfHostSekai.Migrations
{
    /// <inheritdoc />
    public partial class AddAreaCharacterUnlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionSets",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Areas",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "UserAreas",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    AreaId = table.Column<int>(type: "integer", nullable: false),
                    ActionSets = table.Column<string>(type: "jsonb", nullable: false),
                    AreaItems = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PlaylistId = table.Column<int>(type: "integer", nullable: true),
                    PlaylistStatus = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAreas", x => new { x.UserId, x.AreaId });
                    table.ForeignKey(
                        name: "FK_UserAreas_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCharacter",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CharacterId = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Exp = table.Column<int>(type: "integer", nullable: false),
                    TotalExp = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCharacter", x => new { x.UserId, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_UserCharacter_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserUnlock",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    UnlockAt = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUnlock", x => new { x.UserId, x.Category, x.ItemId });
                    table.ForeignKey(
                        name: "FK_UserUnlock_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCharacter_CharacterId",
                table: "UserCharacter",
                column: "CharacterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAreas");

            migrationBuilder.DropTable(
                name: "UserCharacter");

            migrationBuilder.DropTable(
                name: "UserUnlock");

            migrationBuilder.AddColumn<string>(
                name: "ActionSets",
                table: "Users",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Areas",
                table: "Users",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }
    }
}
