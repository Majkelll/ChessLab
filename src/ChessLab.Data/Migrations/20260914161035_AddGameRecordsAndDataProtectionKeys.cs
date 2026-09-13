using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChessLab.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameRecordsAndDataProtectionKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Winner = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    MoveCount = table.Column<int>(type: "integer", nullable: false),
                    MovesSan = table.Column<string>(type: "text", nullable: false),
                    FinalFen = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameRecordPlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BotDifficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameRecordPlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameRecordPlayers_GameRecords_GameRecordId",
                        column: x => x.GameRecordId,
                        principalTable: "GameRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameRecordPlayers_GameRecordId",
                table: "GameRecordPlayers",
                column: "GameRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_GameRecordPlayers_UserId",
                table: "GameRecordPlayers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameRecords_FinishedAtUtc",
                table: "GameRecords",
                column: "FinishedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_GameRecords_RoomCode",
                table: "GameRecords",
                column: "RoomCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "GameRecordPlayers");

            migrationBuilder.DropTable(
                name: "GameRecords");
        }
    }
}
