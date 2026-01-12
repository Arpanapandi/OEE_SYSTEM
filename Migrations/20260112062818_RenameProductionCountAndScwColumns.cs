using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OeeSystem.Migrations
{
    /// <inheritdoc />
    public partial class RenameProductionCountAndScwColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename ProductionCount columns
            migrationBuilder.RenameColumn(
                name: "LotNumber",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "NomorLot");

            migrationBuilder.RenameColumn(
                name: "CompoundName",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "NamaCompound");

            migrationBuilder.RenameColumn(
                name: "ActualWeight",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "BeratAct");

            migrationBuilder.RenameColumn(
                name: "Thinning",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "Penipisan");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "Keterangan");

            migrationBuilder.RenameColumn(
                name: "DurationSeconds",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "DurasiProduksiSeconds");

            // Rename ScwEvent columns
            migrationBuilder.RenameColumn(
                name: "Scw4MTypeId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                newName: "Jenis4MId");

            migrationBuilder.RenameColumn(
                name: "ScwRemarkId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                newName: "JenisRemarkId");

            // Rename indexes if they exist
            migrationBuilder.RenameIndex(
                name: "IX_tb_lwpmixing_ScwEvents_Scw4MTypeId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                newName: "IX_tb_lwpmixing_ScwEvents_Jenis4MId");

            migrationBuilder.RenameIndex(
                name: "IX_tb_lwpmixing_ScwEvents_ScwRemarkId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                newName: "IX_tb_lwpmixing_ScwEvents_JenisRemarkId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback ProductionCount columns
            migrationBuilder.RenameColumn(
                name: "NomorLot",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "LotNumber");

            migrationBuilder.RenameColumn(
                name: "NamaCompound",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "CompoundName");

            migrationBuilder.RenameColumn(
                name: "BeratAct",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "ActualWeight");

            migrationBuilder.RenameColumn(
                name: "Penipisan",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "Thinning");

            migrationBuilder.RenameColumn(
                name: "Keterangan",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "Remarks");

            migrationBuilder.RenameColumn(
                name: "DurasiProduksiSeconds",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                newName: "DurationSeconds");

            // Rollback ScwEvent columns
            migrationBuilder.RenameColumn(
                name: "Jenis4MId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                newName: "Scw4MTypeId");

            migrationBuilder.RenameColumn(
                name: "JenisRemarkId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                newName: "ScwRemarkId");

            // Rollback indexes
            migrationBuilder.RenameIndex(
                name: "IX_tb_lwpmixing_ScwEvents_Jenis4MId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                newName: "IX_tb_lwpmixing_ScwEvents_Scw4MTypeId");

            migrationBuilder.RenameIndex(
                name: "IX_tb_lwpmixing_ScwEvents_JenisRemarkId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                newName: "IX_tb_lwpmixing_ScwEvents_ScwRemarkId");
        }
    }
}
