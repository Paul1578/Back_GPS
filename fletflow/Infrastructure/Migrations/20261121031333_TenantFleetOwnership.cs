using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackEnd_Gps.fletflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TenantFleetOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check legacy relationships before any DDL. MySQL may commit schema
            // statements even when a later command fails, so the check must happen
            // before adding ownership columns or changing indexes.
            const string fallbackOwner = "(SELECT u.Id FROM Users u WHERE u.OwnerUserId IS NULL ORDER BY CASE WHEN EXISTS (SELECT 1 FROM UserRoles ur JOIN Roles r ON r.Id = ur.RoleId WHERE ur.UserId = u.Id AND LOWER(r.Name) = 'admin') THEN 0 ELSE 1 END, u.CreatedAt, u.Id LIMIT 1)";
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS __fletflow_assert_route_owner_consistent;");
            migrationBuilder.Sql($"""
                CREATE PROCEDURE __fletflow_assert_route_owner_consistent()
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM Routes r
                        JOIN Drivers d ON d.Id = r.DriverId
                        LEFT JOIN Users du ON du.Id = d.UserId
                        JOIN Vehicles v ON v.Id = r.VehicleId
                        WHERE COALESCE(du.OwnerUserId, du.Id, {fallbackOwner}) IS NOT NULL
                          AND COALESCE((
                              SELECT COALESCE(vdu.OwnerUserId, vdu.Id, {fallbackOwner})
                              FROM Drivers vd
                              LEFT JOIN Users vdu ON vdu.Id = vd.UserId
                              WHERE vd.VehicleId = v.Id
                                AND COALESCE(vdu.OwnerUserId, vdu.Id, {fallbackOwner}) IS NOT NULL
                              ORDER BY vd.Id
                              LIMIT 1
                          ), {fallbackOwner}) IS NOT NULL
                          AND COALESCE(du.OwnerUserId, du.Id, {fallbackOwner}) <>
                              COALESCE((
                                  SELECT COALESCE(vdu.OwnerUserId, vdu.Id, {fallbackOwner})
                                  FROM Drivers vd
                                  LEFT JOIN Users vdu ON vdu.Id = vd.UserId
                                  WHERE vd.VehicleId = v.Id
                                    AND COALESCE(vdu.OwnerUserId, vdu.Id, {fallbackOwner}) IS NOT NULL
                                  ORDER BY vd.Id
                                  LIMIT 1
                              ), {fallbackOwner})
                    ) THEN
                        SIGNAL SQLSTATE '45000'
                            SET MESSAGE_TEXT = 'TenantFleetOwnership stopped before schema changes: a route links resources from different owners.';
                    END IF;
                END;
                """);
            migrationBuilder.Sql("CALL __fletflow_assert_route_owner_consistent();");
            migrationBuilder.Sql("DROP PROCEDURE __fletflow_assert_route_owner_consistent;");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePositions_RouteId_RecordedAt",
                table: "RoutePositions",
                columns: new[] { "RouteId", "RecordedAt" });

            migrationBuilder.DropIndex(
                name: "IX_RoutePositions_RouteId",
                table: "RoutePositions");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_Plate",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_DocumentNumber",
                table: "Drivers");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Vehicles",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Routes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "Drivers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            // Preserve legacy data by assigning unowned rows to the earliest root admin
            // (falling back to the earliest root account if no root admin exists).
            // Drivers linked to a team user follow that user's company owner.
            migrationBuilder.Sql($"""
                UPDATE Drivers d
                LEFT JOIN Users u ON u.Id = d.UserId
                SET d.OwnerUserId = COALESCE(u.OwnerUserId, u.Id, {fallbackOwner})
                WHERE d.OwnerUserId IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE Vehicles v
                SET v.OwnerUserId = (
                    SELECT d.OwnerUserId FROM Drivers d
                    WHERE d.VehicleId = v.Id AND d.OwnerUserId IS NOT NULL
                    ORDER BY d.Id LIMIT 1
                )
                WHERE v.OwnerUserId IS NULL;
                """);

            migrationBuilder.Sql($"""
                UPDATE Vehicles v
                SET v.OwnerUserId = {fallbackOwner}
                WHERE v.OwnerUserId IS NULL;
                """);

            migrationBuilder.Sql($"""
                UPDATE Routes r
                LEFT JOIN Drivers d ON d.Id = r.DriverId
                LEFT JOIN Vehicles v ON v.Id = r.VehicleId
                SET r.OwnerUserId = CASE
                    WHEN d.OwnerUserId IS NOT NULL AND v.OwnerUserId IS NOT NULL
                         AND d.OwnerUserId <> v.OwnerUserId THEN NULL
                    ELSE COALESCE(d.OwnerUserId, v.OwnerUserId, {fallbackOwner})
                END
                WHERE r.OwnerUserId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_OwnerUserId_Plate",
                table: "Vehicles",
                columns: new[] { "OwnerUserId", "Plate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_OwnerUserId_DocumentNumber",
                table: "Drivers",
                columns: new[] { "OwnerUserId", "DocumentNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_OwnerUserId_Plate",
                table: "Vehicles");

            migrationBuilder.CreateIndex(
                name: "IX_RoutePositions_RouteId",
                table: "RoutePositions",
                column: "RouteId");

            migrationBuilder.DropIndex(
                name: "IX_RoutePositions_RouteId_RecordedAt",
                table: "RoutePositions");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_OwnerUserId_DocumentNumber",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Drivers");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_Plate",
                table: "Vehicles",
                column: "Plate",
                unique: true);


            migrationBuilder.CreateIndex(
                name: "IX_Drivers_DocumentNumber",
                table: "Drivers",
                column: "DocumentNumber",
                unique: true);
        }
    }
}
