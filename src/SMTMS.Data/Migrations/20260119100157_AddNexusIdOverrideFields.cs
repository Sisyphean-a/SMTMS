using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMTMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNexusIdOverrideFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalNexusId",
                table: "ModMetadata",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OverrideNexusId",
                table: "ModMetadata",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalNexusId",
                table: "ModMetadata");

            migrationBuilder.DropColumn(
                name: "OverrideNexusId",
                table: "ModMetadata");
        }
    }
}
