using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeApi.Migrations
{
    /// <inheritdoc />
    public partial class AddBeanHopperOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BeanHopperOverrides",
                columns: table => new
                {
                    SnapshotId = table.Column<int>(type: "INTEGER", nullable: false),
                    Counter = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BeanHopper = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeanHopperOverrides", x => new { x.SnapshotId, x.Counter });
                    table.ForeignKey(
                        name: "FK_BeanHopperOverrides_MachineSnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "MachineSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeanHopperOverrides");
        }
    }
}
