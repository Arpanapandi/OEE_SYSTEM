using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OeeSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentStateToJobRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentState",
                table: "tb_lwpmixing_JobRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentState",
                table: "tb_lwpmixing_JobRuns");
        }
    }
}
