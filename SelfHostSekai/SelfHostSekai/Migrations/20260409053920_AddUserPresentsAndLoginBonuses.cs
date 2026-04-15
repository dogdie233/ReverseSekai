using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SelfHostSekai.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPresentsAndLoginBonuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserLoginBonuses",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginBonusType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LoginBonusId = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<long>(type: "bigint", nullable: false),
                    DisplayTexts = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginBonuses", x => new { x.UserId, x.LoginBonusType, x.LoginBonusId });
                    table.ForeignKey(
                        name: "FK_UserLoginBonuses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPresents",
                columns: table => new
                {
                    PresentId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<int>(type: "integer", nullable: false),
                    ResourceLevel = table.Column<int>(type: "integer", nullable: false),
                    ResourceQuantity = table.Column<int>(type: "integer", nullable: false),
                    ExpiredAt = table.Column<long>(type: "bigint", nullable: true),
                    GrantedAt = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPresents", x => x.PresentId);
                    table.ForeignKey(
                        name: "FK_UserPresents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPresents_UserId",
                table: "UserPresents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserLoginBonuses");

            migrationBuilder.DropTable(
                name: "UserPresents");
        }
    }
}
