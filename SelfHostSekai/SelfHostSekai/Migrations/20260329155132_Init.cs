using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SelfHostSekai.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Exp = table.Column<int>(type: "integer", nullable: false),
                    TotalExp = table.Column<int>(type: "integer", nullable: false),
                    Coin = table.Column<int>(type: "integer", nullable: false),
                    VirtualCoin = table.Column<int>(type: "integer", nullable: false),
                    CurrentDeckNumber = table.Column<int>(type: "integer", nullable: false),
                    RegistrationInfo = table.Column<string>(type: "jsonb", nullable: true),
                    Config = table.Column<string>(type: "jsonb", nullable: true),
                    Currency = table.Column<string>(type: "jsonb", nullable: true),
                    BoostInfo = table.Column<string>(type: "jsonb", nullable: true),
                    TutorialInfo = table.Column<string>(type: "jsonb", nullable: true),
                    ChallengeLivePlayDay = table.Column<string>(type: "jsonb", nullable: true),
                    EventBreakTime = table.Column<string>(type: "jsonb", nullable: true),
                    Profile = table.Column<string>(type: "jsonb", nullable: true),
                    ViewableAppeal = table.Column<string>(type: "jsonb", nullable: true),
                    Avatar = table.Column<string>(type: "jsonb", nullable: true),
                    AutoLive = table.Column<string>(type: "jsonb", nullable: true),
                    Areas = table.Column<string>(type: "jsonb", nullable: false),
                    ActionSets = table.Column<string>(type: "jsonb", nullable: false),
                    UnitEpisodeStatuses = table.Column<string>(type: "jsonb", nullable: false),
                    SpecialEpisodeStatuses = table.Column<string>(type: "jsonb", nullable: false),
                    CharacterProfileEpisodeStatuses = table.Column<string>(type: "jsonb", nullable: false),
                    UnreadTopics = table.Column<string>(type: "jsonb", nullable: false),
                    Shops = table.Column<string>(type: "jsonb", nullable: false),
                    CharacterMissions = table.Column<string>(type: "jsonb", nullable: false),
                    CharacterMissionStatuses = table.Column<string>(type: "jsonb", nullable: false),
                    Events = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserCards",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CardId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    MasterRank = table.Column<int>(type: "integer", nullable: false),
                    SpecialTrainingStatus = table.Column<int>(type: "integer", nullable: false),
                    DefaultImage = table.Column<int>(type: "integer", nullable: false),
                    SkillLevel = table.Column<int>(type: "integer", nullable: false),
                    Exp = table.Column<int>(type: "integer", nullable: false),
                    TotalExp = table.Column<int>(type: "integer", nullable: false),
                    SkillExp = table.Column<int>(type: "integer", nullable: false),
                    TotalSkillExp = table.Column<int>(type: "integer", nullable: false),
                    DuplicateCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    IsNew = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCards", x => new { x.UserId, x.CardId });
                    table.ForeignKey(
                        name: "FK_UserCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDecks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DeckId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Member1 = table.Column<int>(type: "integer", nullable: false),
                    Member2 = table.Column<int>(type: "integer", nullable: false),
                    Member3 = table.Column<int>(type: "integer", nullable: false),
                    Member4 = table.Column<int>(type: "integer", nullable: false),
                    Member5 = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDecks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserItems",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserItems", x => new { x.UserId, x.ItemType, x.ItemId });
                    table.ForeignKey(
                        name: "FK_UserItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMusicResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    MusicId = table.Column<int>(type: "integer", nullable: false),
                    MusicDifficulty = table.Column<int>(type: "integer", nullable: false),
                    PlayType = table.Column<int>(type: "integer", nullable: false),
                    HighScore = table.Column<int>(type: "integer", nullable: false),
                    IsClear = table.Column<bool>(type: "boolean", nullable: false),
                    IsFullCombo = table.Column<bool>(type: "boolean", nullable: false),
                    IsAllPerfect = table.Column<bool>(type: "boolean", nullable: false),
                    MaxCombo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMusicResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMusicResults_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMusics",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    VocalId = table.Column<int>(type: "integer", nullable: false),
                    MusicId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMusics", x => new { x.UserId, x.VocalId });
                    table.ForeignKey(
                        name: "FK_UserMusics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDecks_UserId",
                table: "UserDecks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMusicResults_UserId",
                table: "UserMusicResults",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCards");

            migrationBuilder.DropTable(
                name: "UserDecks");

            migrationBuilder.DropTable(
                name: "UserItems");

            migrationBuilder.DropTable(
                name: "UserMusicResults");

            migrationBuilder.DropTable(
                name: "UserMusics");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
