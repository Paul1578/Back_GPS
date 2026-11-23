using fletflow.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackEnd_Gps.fletflow.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261123132833_GuardDiscordantRouteOwnership")]
public sealed class GuardDiscordantRouteOwnership : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The tenant ownership migration may already have run in an environment where
        // it assigned NULL to a cross-tenant route. Fail startup visibly so an operator
        // can reconcile the route instead of leaving it silently hidden by tenant filters.
        migrationBuilder.Sql("DROP PROCEDURE IF EXISTS __fletflow_assert_existing_route_owner;");
        migrationBuilder.Sql("""
            CREATE PROCEDURE __fletflow_assert_existing_route_owner()
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM Routes r
                    JOIN Drivers d ON d.Id = r.DriverId
                    JOIN Vehicles v ON v.Id = r.VehicleId
                    WHERE r.OwnerUserId IS NULL
                      AND d.OwnerUserId IS NOT NULL
                      AND v.OwnerUserId IS NOT NULL
                      AND d.OwnerUserId <> v.OwnerUserId
                ) THEN
                    SIGNAL SQLSTATE '45000'
                        SET MESSAGE_TEXT = 'Route ownership check failed: reconcile routes linked to different tenant owners before starting FletFlow.';
                END IF;
            END;
            """);
        migrationBuilder.Sql("CALL __fletflow_assert_existing_route_owner();");
        migrationBuilder.Sql("DROP PROCEDURE __fletflow_assert_existing_route_owner;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
