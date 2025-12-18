using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMTMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LastModsDirectory = table.Column<string>(type: "TEXT", nullable: true),
                    WindowWidth = table.Column<int>(type: "INTEGER", nullable: false),
                    WindowHeight = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoScanOnStartup = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModMetadata",
                columns: table => new
                {
                    UniqueID = table.Column<string>(type: "TEXT", nullable: false),
                    UserCategory = table.Column<string>(type: "TEXT", nullable: true),
                    NexusSummary = table.Column<string>(type: "TEXT", nullable: true),
                    NexusDescription = table.Column<string>(type: "TEXT", nullable: true),
                    NexusImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    NexusDownloadCount = table.Column<long>(type: "INTEGER", nullable: true),
                    NexusEndorsementCount = table.Column<long>(type: "INTEGER", nullable: true),
                    LastNexusCheck = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginalName = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalDescription = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedName = table.Column<string>(type: "TEXT", nullable: true),
                    TranslatedDescription = table.Column<string>(type: "TEXT", nullable: true),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: true),
                    IsMachineTranslated = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastTranslationUpdate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModMetadata", x => x.UniqueID);
                });

            migrationBuilder.CreateTable(
                name: "TranslationMemory",
                columns: table => new
                {
                    SourceHash = table.Column<string>(type: "TEXT", nullable: false),
                    SourceText = table.Column<string>(type: "TEXT", nullable: false),
                    TargetText = table.Column<string>(type: "TEXT", nullable: false),
                    Engine = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationMemory", x => x.SourceHash);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ModMetadata");

            migrationBuilder.DropTable(
                name: "TranslationMemory");
        }
    }
}
