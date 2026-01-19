using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMTMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyNexusIdFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalNexusId",
                table: "ModMetadata");

            migrationBuilder.DropColumn(
                name: "OverrideNexusId",
                table: "ModMetadata");

            migrationBuilder.AddColumn<bool>(
                name: "IsNexusIdUserAdded",
                table: "ModMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNexusIdUserAdded",
                table: "ModMetadata");

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
    }
}
