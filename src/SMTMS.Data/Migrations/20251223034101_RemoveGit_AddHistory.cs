using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMTMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGit_AddHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitDiffCache");

            migrationBuilder.AddColumn<string>(
                name: "CurrentJson",
                table: "ModMetadata",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalJson",
                table: "ModMetadata",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HistorySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    TotalMods = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorySnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModTranslationHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                    ModUniqueId = table.Column<string>(type: "TEXT", nullable: false),
                    JsonContent = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousHash = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModTranslationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModTranslationHistories_HistorySnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "HistorySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModTranslationHistories_ModMetadata_ModUniqueId",
                        column: x => x.ModUniqueId,
                        principalTable: "ModMetadata",
                        principalColumn: "UniqueID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorySnapshots_Timestamp",
                table: "HistorySnapshots",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ModTranslationHistories_ModUniqueId",
                table: "ModTranslationHistories",
                column: "ModUniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_ModTranslationHistories_SnapshotId",
                table: "ModTranslationHistories",
                column: "SnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModTranslationHistories");

            migrationBuilder.DropTable(
                name: "HistorySnapshots");

            migrationBuilder.DropColumn(
                name: "CurrentJson",
                table: "ModMetadata");

            migrationBuilder.DropColumn(
                name: "OriginalJson",
                table: "ModMetadata");

            migrationBuilder.CreateTable(
                name: "GitDiffCache",
                columns: table => new
                {
                    CommitHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FormatVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ModCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SerializedDiffData = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitDiffCache", x => x.CommitHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitDiffCache_CreatedAt",
                table: "GitDiffCache",
                column: "CreatedAt");
        }
    }
}
