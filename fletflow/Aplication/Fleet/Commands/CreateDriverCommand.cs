using System;
using System.Linq;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class CreateDriverCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IDriverRepository _drivers;

        public CreateDriverCommand(IUnitOfWork uow, IDriverRepository drivers)
        {
            _uow = uow;
            _drivers = drivers;
        }

        public async Task<DriverDto> Execute(
            string firstName,
            string lastName,
            string documentNumber,
            string phoneNumber,
            Guid? userId = null,
            Guid? ownerUserId = null)
        {
            firstName = firstName?.Trim() ?? string.Empty;
            lastName = lastName?.Trim() ?? string.Empty;
            documentNumber = documentNumber.Trim();
            phoneNumber = phoneNumber?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(documentNumber) || string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("Nombres, apellidos, documento y teléfono son obligatorios.");
            if (documentNumber.Length > 20 || phoneNumber.Length > 20)
                throw new ArgumentException("Documento y teléfono no pueden exceder 20 caracteres.");

            var existing = await _drivers.GetByDocumentAsync(documentNumber);
            if (existing is not null)
                throw new BusinessConflictException($"Ya existe un conductor con el documento {documentNumber} en esta empresa.");

            if (userId.HasValue)
            {
                var user = await _uow.Users.GetByIdAsync(userId.Value)
                    ?? throw new KeyNotFoundException("User not found to link as driver.");

                if (!user.IsActive)
                    throw new InvalidOperationException("No se puede vincular un usuario inactivo como conductor.");

                if (!ownerUserId.HasValue || (user.OwnerUserId ?? user.Id) != ownerUserId.Value)
                    throw new KeyNotFoundException("User not found to link as driver.");

                var driverForUser = await _drivers.GetByUserIdAsync(userId.Value);
                if (driverForUser is not null)
                    throw new InvalidOperationException("This user is already linked to a driver.");

                var hasDriverRole = user.UserRoles.Any(ur => ur.Role.Name.Trim().Equals("Driver", StringComparison.OrdinalIgnoreCase));
                if (!hasDriverRole)
                    throw new InvalidOperationException("The user does not have the Driver role.");
            }

            var driver = Driver.Create(firstName, lastName, documentNumber, phoneNumber, userId);

            await _drivers.AddAsync(driver);
            await _uow.CommitAsync();

            return DriverApplicationMapper.ToDto(driver);
        }
    }
}
