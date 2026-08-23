using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEstimatedSnapshotFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEstimated",
                table: "MachineSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEstimated",
                table: "MachineSnapshots");
        }
    }
}
