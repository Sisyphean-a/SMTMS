using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMTMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGitDiffCacheAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitDiffCache",
                columns: table => new
                {
                    CommitHash = table.Column<string>(type: "TEXT", nullable: false),
                    SerializedDiffData = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ModCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FormatVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitDiffCache", x => x.CommitHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationMemory_Engine",
                table: "TranslationMemory",
                column: "Engine");

            migrationBuilder.CreateIndex(
                name: "IX_TranslationMemory_Timestamp",
                table: "TranslationMemory",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ModMetadata_LastTranslationUpdate",
                table: "ModMetadata",
                column: "LastTranslationUpdate");

            migrationBuilder.CreateIndex(
                name: "IX_ModMetadata_RelativePath",
                table: "ModMetadata",
                column: "RelativePath");

            migrationBuilder.CreateIndex(
                name: "IX_GitDiffCache_CreatedAt",
                table: "GitDiffCache",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitDiffCache");

            migrationBuilder.DropIndex(
                name: "IX_TranslationMemory_Engine",
                table: "TranslationMemory");

            migrationBuilder.DropIndex(
                name: "IX_TranslationMemory_Timestamp",
                table: "TranslationMemory");

            migrationBuilder.DropIndex(
                name: "IX_ModMetadata_LastTranslationUpdate",
                table: "ModMetadata");

            migrationBuilder.DropIndex(
                name: "IX_ModMetadata_RelativePath",
                table: "ModMetadata");
        }
    }
}
