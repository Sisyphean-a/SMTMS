using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMTMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeIndexForHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ModTranslationHistories_ModUniqueId_SnapshotId",
                table: "ModTranslationHistories",
                columns: new[] { "ModUniqueId", "SnapshotId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModTranslationHistories_ModUniqueId_SnapshotId",
                table: "ModTranslationHistories");
        }
    }
}
