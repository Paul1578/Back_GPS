using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackEnd_Gps.fletflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RouteLifecycleAndSingleRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "Routes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Routes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Routes");
        }
    }
}
