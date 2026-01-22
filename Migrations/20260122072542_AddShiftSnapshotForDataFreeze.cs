using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OeeSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftSnapshotForDataFreeze : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_ShiftSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    ShiftId = table.Column<int>(type: "INTEGER", nullable: false),
                    ShiftDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ShiftKey = table.Column<string>(type: "TEXT", nullable: false),
                    FinalOee = table.Column<double>(type: "REAL", nullable: false),
                    FinalAvailability = table.Column<double>(type: "REAL", nullable: false),
                    FinalPerformance = table.Column<double>(type: "REAL", nullable: false),
                    FinalQuality = table.Column<double>(type: "REAL", nullable: false),
                    PlannedProductionTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    OperatingTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    DowntimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    RestBreakTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    NoLoadingTimeSeconds = table.Column<double>(type: "REAL", nullable: false),
                    TotalGood = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalReject = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UnlockedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UnlockedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UnlockReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_ShiftSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ShiftSnapshots_tb_lwpmixing_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "tb_lwpmixing_Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ShiftSnapshots_tb_lwpmixing_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "tb_lwpmixing_Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ShiftSnapshots_tb_lwpmixing_Users_LockedByUserId",
                        column: x => x.LockedByUserId,
                        principalTable: "tb_lwpmixing_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ShiftSnapshots_tb_lwpmixing_Users_UnlockedByUserId",
                        column: x => x.UnlockedByUserId,
                        principalTable: "tb_lwpmixing_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ShiftSnapshots_LockedByUserId",
                table: "tb_lwpmixing_ShiftSnapshots",
                column: "LockedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ShiftSnapshots_MachineId_ShiftId_ShiftDate",
                table: "tb_lwpmixing_ShiftSnapshots",
                columns: new[] { "MachineId", "ShiftId", "ShiftDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ShiftSnapshots_ShiftId",
                table: "tb_lwpmixing_ShiftSnapshots",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ShiftSnapshots_UnlockedByUserId",
                table: "tb_lwpmixing_ShiftSnapshots",
                column: "UnlockedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_lwpmixing_ShiftSnapshots");
        }
    }
}
