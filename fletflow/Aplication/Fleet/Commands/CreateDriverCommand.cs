using System;
using System.Linq;
using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
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
            Guid? userId = null)
        {
            documentNumber = documentNumber.Trim();

            var existing = await _drivers.GetByDocumentAsync(documentNumber);
            if (existing is not null)
                throw new InvalidOperationException($"Ya existe un conductor con documento {documentNumber}.");

            if (userId.HasValue)
            {
                var user = await _uow.Users.GetByIdAsync(userId.Value)
                    ?? throw new KeyNotFoundException("Usuario no encontrado para vincular como driver.");

                var driverForUser = await _drivers.GetByUserIdAsync(userId.Value);
                if (driverForUser is not null)
                    throw new InvalidOperationException("Este usuario ya está vinculado a un conductor.");

                var hasDriverRole = user.UserRoles.Any(ur => ur.Role.Name.Trim().Equals("Driver", StringComparison.OrdinalIgnoreCase));
                if (!hasDriverRole)
                    throw new InvalidOperationException("El usuario no tiene rol Driver.");
            }

            var driver = Driver.Create(firstName, lastName, documentNumber, phoneNumber, userId);

            await _drivers.AddAsync(driver);
            await _uow.CommitAsync();

            return DriverApplicationMapper.ToDto(driver);
        }
    }
}
