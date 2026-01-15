using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OeeSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDowntimeEventFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "produksi");

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_DowntimeReasons",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPlanned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_DowntimeReasons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_Komponens",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JmlKomponen = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_Komponens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_ManPowers",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_ManPowers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_NgTypes",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_NgTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_Plants",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_Plants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_Scw4MTypes",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_Scw4MTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_Shifts",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_Shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_Users",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProfileImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_Machines",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LineId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlantId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManufacturingYear = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstallationYear = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_Machines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_Machines_tb_lwpmixing_Plants_PlantId",
                        column: x => x.PlantId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Plants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_Products",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UoM = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SLOC = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlantId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StandarCycleTime = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_Products_tb_lwpmixing_Plants_PlantId",
                        column: x => x.PlantId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Plants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_ScwRemarks",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Scw4MTypeId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_ScwRemarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ScwRemarks_tb_lwpmixing_Scw4MTypes_Scw4MTypeId",
                        column: x => x.Scw4MTypeId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Scw4MTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_MachineDowntimeReasons",
                schema: "produksi",
                columns: table => new
                {
                    MachineId = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    DowntimeReasonId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_MachineDowntimeReasons", x => new { x.MachineId, x.DowntimeReasonId });
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_MachineDowntimeReasons_tb_lwpmixing_DowntimeReasons_DowntimeReasonId",
                        column: x => x.DowntimeReasonId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_DowntimeReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_MachineDowntimeReasons_tb_lwpmixing_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_ProductMachines",
                schema: "produksi",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    MachineId = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_ProductMachines", x => new { x.ProductId, x.MachineId });
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ProductMachines_tb_lwpmixing_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ProductMachines_tb_lwpmixing_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_ProductNgTypes",
                schema: "produksi",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    NgTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_ProductNgTypes", x => new { x.ProductId, x.NgTypeId });
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ProductNgTypes_tb_lwpmixing_NgTypes_NgTypeId",
                        column: x => x.NgTypeId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_NgTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ProductNgTypes_tb_lwpmixing_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_WorkOrders",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    TargetQuantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlannedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShiftId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_WorkOrders_tb_lwpmixing_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_WorkOrders_tb_lwpmixing_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Shifts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_JobRuns",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineId = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    WorkOrderId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStatusChangeTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OperatorId = table.Column<int>(type: "int", nullable: false),
                    ManPowerId = table.Column<int>(type: "int", nullable: true),
                    DandoriStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DandoriEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DandoriDurationSeconds = table.Column<int>(type: "int", nullable: true),
                    ScannedPartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScannedLotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScannedKomponenId = table.Column<int>(type: "int", nullable: true),
                    ScannedJmlKomponen = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_JobRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_JobRuns_tb_lwpmixing_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_JobRuns_tb_lwpmixing_ManPowers_ManPowerId",
                        column: x => x.ManPowerId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_ManPowers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_JobRuns_tb_lwpmixing_Users_OperatorId",
                        column: x => x.OperatorId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_JobRuns_tb_lwpmixing_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_DowntimeEvents",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobRunId = table.Column<int>(type: "int", nullable: false),
                    ReasonId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: false),
                    IsRestBreak = table.Column<bool>(type: "bit", nullable: false),
                    IsNoLoading = table.Column<bool>(type: "bit", nullable: false),
                    IsLineStop = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_DowntimeEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_DowntimeEvents_tb_lwpmixing_DowntimeReasons_ReasonId",
                        column: x => x.ReasonId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_DowntimeReasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_DowntimeEvents_tb_lwpmixing_JobRuns_JobRunId",
                        column: x => x.JobRunId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_JobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_ProductionCounts",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobRunId = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GoodCount = table.Column<int>(type: "int", nullable: false),
                    RejectCount = table.Column<int>(type: "int", nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NgTypeId = table.Column<int>(type: "int", nullable: true),
                    InjectionGroup = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NomorLot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LotBo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NamaCompound = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BeratAct = table.Column<double>(type: "float", nullable: true),
                    Penipisan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keterangan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManPowerId = table.Column<int>(type: "int", nullable: true),
                    ComponentId = table.Column<int>(type: "int", nullable: true),
                    DurasiProduksiSeconds = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_ProductionCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ProductionCounts_tb_lwpmixing_JobRuns_JobRunId",
                        column: x => x.JobRunId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_JobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ProductionCounts_tb_lwpmixing_Komponens_ComponentId",
                        column: x => x.ComponentId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Komponens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ProductionCounts_tb_lwpmixing_ManPowers_ManPowerId",
                        column: x => x.ManPowerId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_ManPowers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ProductionCounts_tb_lwpmixing_NgTypes_NgTypeId",
                        column: x => x.NgTypeId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_NgTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tb_lwpmixing_ScwEvents",
                schema: "produksi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobRunId = table.Column<int>(type: "int", nullable: false),
                    Jenis4MId = table.Column<int>(type: "int", nullable: false),
                    JenisRemarkId = table.Column<int>(type: "int", nullable: false),
                    MachineId = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: false),
                    AdditionalNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_lwpmixing_ScwEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ScwEvents_tb_lwpmixing_JobRuns_JobRunId",
                        column: x => x.JobRunId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_JobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ScwEvents_tb_lwpmixing_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ScwEvents_tb_lwpmixing_Scw4MTypes_Jenis4MId",
                        column: x => x.Jenis4MId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_Scw4MTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_lwpmixing_ScwEvents_tb_lwpmixing_ScwRemarks_JenisRemarkId",
                        column: x => x.JenisRemarkId,
                        principalSchema: "produksi",
                        principalTable: "tb_lwpmixing_ScwRemarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "produksi",
                table: "tb_lwpmixing_Plants",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[] { 1, "PLT01", "Plant Dummy" });

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_DowntimeEvents_JobRunId",
                schema: "produksi",
                table: "tb_lwpmixing_DowntimeEvents",
                column: "JobRunId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_DowntimeEvents_ReasonId",
                schema: "produksi",
                table: "tb_lwpmixing_DowntimeEvents",
                column: "ReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_JobRuns_MachineId",
                schema: "produksi",
                table: "tb_lwpmixing_JobRuns",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_JobRuns_ManPowerId",
                schema: "produksi",
                table: "tb_lwpmixing_JobRuns",
                column: "ManPowerId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_JobRuns_OperatorId",
                schema: "produksi",
                table: "tb_lwpmixing_JobRuns",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_JobRuns_WorkOrderId",
                schema: "produksi",
                table: "tb_lwpmixing_JobRuns",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_MachineDowntimeReasons_DowntimeReasonId",
                schema: "produksi",
                table: "tb_lwpmixing_MachineDowntimeReasons",
                column: "DowntimeReasonId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_Machines_PlantId",
                schema: "produksi",
                table: "tb_lwpmixing_Machines",
                column: "PlantId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ProductionCounts_ComponentId",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ProductionCounts_JobRunId",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                column: "JobRunId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ProductionCounts_ManPowerId",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                column: "ManPowerId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ProductionCounts_NgTypeId",
                schema: "produksi",
                table: "tb_lwpmixing_ProductionCounts",
                column: "NgTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ProductMachines_MachineId",
                schema: "produksi",
                table: "tb_lwpmixing_ProductMachines",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ProductNgTypes_NgTypeId",
                schema: "produksi",
                table: "tb_lwpmixing_ProductNgTypes",
                column: "NgTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_Products_PlantId",
                schema: "produksi",
                table: "tb_lwpmixing_Products",
                column: "PlantId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ScwEvents_Jenis4MId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                column: "Jenis4MId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ScwEvents_JenisRemarkId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                column: "JenisRemarkId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ScwEvents_JobRunId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                column: "JobRunId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ScwEvents_MachineId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwEvents",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_ScwRemarks_Scw4MTypeId",
                schema: "produksi",
                table: "tb_lwpmixing_ScwRemarks",
                column: "Scw4MTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_WorkOrders_ProductId",
                schema: "produksi",
                table: "tb_lwpmixing_WorkOrders",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_tb_lwpmixing_WorkOrders_ShiftId",
                schema: "produksi",
                table: "tb_lwpmixing_WorkOrders",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_lwpmixing_DowntimeEvents",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_MachineDowntimeReasons",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_ProductionCounts",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_ProductMachines",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_ProductNgTypes",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_ScwEvents",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_DowntimeReasons",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_Komponens",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_NgTypes",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_JobRuns",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_ScwRemarks",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_Machines",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_ManPowers",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_Users",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_WorkOrders",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_Scw4MTypes",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_Products",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_Shifts",
                schema: "produksi");

            migrationBuilder.DropTable(
                name: "tb_lwpmixing_Plants",
                schema: "produksi");
        }
    }
}
