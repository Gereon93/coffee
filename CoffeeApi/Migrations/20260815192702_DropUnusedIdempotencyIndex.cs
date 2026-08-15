using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeApi.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedIdempotencyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MachineSnapshots_Idempotency",
                table: "MachineSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MachineSnapshots_Idempotency",
                table: "MachineSnapshots",
                columns: new[] { "MachineId", "BeverageCounterCoffee", "BeverageCounterCoffeeAndMilk", "BeverageCounterMilk" });
        }
    }
}
