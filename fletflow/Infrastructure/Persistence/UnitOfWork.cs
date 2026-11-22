using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace fletflow.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _routeAssignmentTransaction;

        public IUserRepository Users { get; }
        public IRoleRepository Roles { get; }
        public IRefreshTokenRepository RefreshTokens { get; }
        public IVehicleRepository Vehicles { get; }
        public IDriverRepository Drivers { get; }
        public IRouteRepository Routes { get; }
        public IRoutePositionRepository RoutePositions { get; }

        public UnitOfWork(
            AppDbContext context,
            IUserRepository users,
            IRoleRepository roles,
            IRefreshTokenRepository rts,
            IPasswordResetTokenRepository passwordResetTokens,
            IVehicleRepository vehicleRepository,
            IDriverRepository drivers,
            IRouteRepository routes,
            IRoutePositionRepository routePositions)
        {
            _context = context;
            Users = users;
            Roles = roles;
            RefreshTokens = rts;
            PasswordResetTokens = passwordResetTokens;
            Vehicles = vehicleRepository;
            Drivers = drivers;
            Routes = routes;
            RoutePositions = routePositions;
        }

        public async Task<int> CommitAsync()
        {
            var result = await _context.SaveChangesAsync();
            if (_routeAssignmentTransaction is not null)
            {
                await _routeAssignmentTransaction.CommitAsync();
                await _routeAssignmentTransaction.DisposeAsync();
                _routeAssignmentTransaction = null;
            }
            return result;
        }

        public IPasswordResetTokenRepository PasswordResetTokens { get; }

        public async Task LockRouteAssignmentAsync(Guid? vehicleId, Guid? driverId, Guid? routeId = null)
        {
            if (_routeAssignmentTransaction is not null)
                throw new InvalidOperationException("Ya existe una transacción de asignación de ruta activa.");

            if (!_context.Database.IsRelational())
                return;

            _routeAssignmentTransaction = await _context.Database.BeginTransactionAsync();

            // Los tests con SQLite conservan cobertura funcional, pero el lock de filas
            // que garantiza exclusión mutua es específico de MySQL/InnoDB.
            if (!_context.Database.ProviderName!.Contains("MySql", StringComparison.OrdinalIgnoreCase))
            {
                if (routeId.HasValue)
                {
                    var routeAssignment = await _context.Routes.AsNoTracking()
                        .Where(route => route.Id == routeId.Value)
                        .Select(route => new { route.VehicleId, route.DriverId })
                        .FirstOrDefaultAsync()
                        ?? throw new KeyNotFoundException("Route not found.");
                    vehicleId ??= routeAssignment.VehicleId;
                    driverId ??= routeAssignment.DriverId;
                }
                return;
            }

            var ownerId = _context.CurrentOwnerUserId
                ?? throw new InvalidOperationException("No se puede bloquear recursos de ruta sin propietario autenticado.");

            (Guid VehicleId, Guid DriverId)? currentRouteAssignment = null;
            if (routeId.HasValue)
            {
                currentRouteAssignment = await LockOwnerScopedRouteAsync(routeId.Value, ownerId)
                    ?? throw new KeyNotFoundException("Route not found.");
            }

            // Todas las operaciones toman primero Routes (si aplica), luego Vehicle
            // ordenados por ID y finalmente Driver ordenados por ID.
            var vehicleIds = new[] { vehicleId }
                .Concat(currentRouteAssignment.HasValue ? new Guid?[] { currentRouteAssignment.Value.VehicleId } : Array.Empty<Guid?>())
                .OfType<Guid>()
                .Distinct()
                .OrderBy(id => id);
            var driverIds = new[] { driverId }
                .Concat(currentRouteAssignment.HasValue ? new Guid?[] { currentRouteAssignment.Value.DriverId } : Array.Empty<Guid?>())
                .OfType<Guid>()
                .Distinct()
                .OrderBy(id => id);

            foreach (var id in vehicleIds)
                if (!await LockOwnerScopedRowAsync("Vehicles", id, ownerId))
                    throw new KeyNotFoundException("Vehicle not found.");

            foreach (var id in driverIds)
                if (!await LockOwnerScopedRowAsync("Drivers", id, ownerId))
                    throw new KeyNotFoundException("Driver not found.");
        }

        private async Task<(Guid VehicleId, Guid DriverId)?> LockOwnerScopedRouteAsync(Guid routeId, Guid ownerId)
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.Transaction = _routeAssignmentTransaction!.GetDbTransaction();
            command.CommandText = "SELECT `VehicleId`, `DriverId` FROM `Routes` WHERE `Id` = @id AND `OwnerUserId` = @ownerId FOR UPDATE";

            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "@id";
            idParameter.DbType = DbType.Guid;
            idParameter.Value = routeId;
            command.Parameters.Add(idParameter);

            var ownerParameter = command.CreateParameter();
            ownerParameter.ParameterName = "@ownerId";
            ownerParameter.DbType = DbType.Guid;
            ownerParameter.Value = ownerId;
            command.Parameters.Add(ownerParameter);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;
            return (reader.GetGuid(0), reader.GetGuid(1));
        }

        private async Task<bool> LockOwnerScopedRowAsync(string table, Guid id, Guid ownerId)
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.Transaction = _routeAssignmentTransaction!.GetDbTransaction();
            command.CommandText = $"SELECT `Id` FROM `{table}` WHERE `Id` = @id AND `OwnerUserId` = @ownerId FOR UPDATE";

            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "@id";
            idParameter.DbType = DbType.Guid;
            idParameter.Value = id;
            command.Parameters.Add(idParameter);

            var ownerParameter = command.CreateParameter();
            ownerParameter.ParameterName = "@ownerId";
            ownerParameter.DbType = DbType.Guid;
            ownerParameter.Value = ownerId;
            command.Parameters.Add(ownerParameter);

            return await command.ExecuteScalarAsync() is not null;
        }

        public void Rollback()
        {
            if (_routeAssignmentTransaction is null)
                return;

            try
            {
                _routeAssignmentTransaction.Rollback();
            }
            finally
            {
                _routeAssignmentTransaction.Dispose();
                _routeAssignmentTransaction = null;
            }
        }
    }
}
