using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackEnd_Gps.fletflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteCreatedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "Routes",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "Routes");
        }
    }
}
