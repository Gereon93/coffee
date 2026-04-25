using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeApi.Migrations
{
    /// <inheritdoc />
    public partial class RenameExcludedDaysToMarkedDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename table
            migrationBuilder.RenameTable(
                name: "ExcludedDays",
                newName: "MarkedDays");

            // Add Kind column with default "mass-import" (existing rows get this)
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "MarkedDays",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "mass-import");

            // Add nullable EventType column
            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "MarkedDays",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EventType", table: "MarkedDays");
            migrationBuilder.DropColumn(name: "Kind",      table: "MarkedDays");
            migrationBuilder.RenameTable(name: "MarkedDays", newName: "ExcludedDays");
        }
    }
}
