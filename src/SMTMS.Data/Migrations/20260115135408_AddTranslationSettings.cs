using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMTMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TranslationApiType",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "Google");

            migrationBuilder.AddColumn<string>(
                name: "TranslationSourceLang",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "auto");

            migrationBuilder.AddColumn<string>(
                name: "TranslationTargetLang",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "zh-CN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TranslationApiType",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "TranslationSourceLang",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "TranslationTargetLang",
                table: "AppSettings");
        }
    }
}
