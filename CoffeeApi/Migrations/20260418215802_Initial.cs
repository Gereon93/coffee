using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeApi.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MachineSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "EQ900-DEFAULT"),
                    BeverageCounterCoffee = table.Column<int>(type: "INTEGER", nullable: false),
                    BeverageCounterCoffeeAndMilk = table.Column<int>(type: "INTEGER", nullable: false),
                    BeverageCounterMilk = table.Column<int>(type: "INTEGER", nullable: false),
                    BeverageCounterHotWaterCups = table.Column<int>(type: "INTEGER", nullable: false),
                    BeverageCounterHotWater = table.Column<int>(type: "INTEGER", nullable: false),
                    OperationState = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RemoteControlAllowed = table.Column<bool>(type: "INTEGER", nullable: false),
                    LocalControlActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    InteriorIlluminationActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineSnapshots_Idempotency",
                table: "MachineSnapshots",
                columns: new[] { "MachineId", "BeverageCounterCoffee", "BeverageCounterCoffeeAndMilk", "BeverageCounterMilk" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineSnapshots_MachineId",
                table: "MachineSnapshots",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineSnapshots_Timestamp",
                table: "MachineSnapshots",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MachineSnapshots");
        }
    }
}
