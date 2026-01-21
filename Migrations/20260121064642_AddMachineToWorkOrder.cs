using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OeeSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineToWorkOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MachineId",
                table: "tb_lwpmixing_WorkOrders",
                type: "TEXT",
                maxLength: 4,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_WorkOrders_MachineId",
                table: "tb_lwpmixing_WorkOrders",
                column: "MachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_tb_lwpmixing_WorkOrders_tb_lwpmixing_Machines_MachineId",
                table: "tb_lwpmixing_WorkOrders",
                column: "MachineId",
                principalTable: "tb_lwpmixing_Machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tb_lwpmixing_WorkOrders_tb_lwpmixing_Machines_MachineId",
                table: "tb_lwpmixing_WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_tb_lwpmixing_WorkOrders_MachineId",
                table: "tb_lwpmixing_WorkOrders");

            migrationBuilder.DropColumn(
                name: "MachineId",
                table: "tb_lwpmixing_WorkOrders");
        }
    }
}
