using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Application.Fleet.Commands;

public class UpdateDriverCommand
{
    private readonly IUnitOfWork _uow;
    private readonly IDriverRepository _drivers;
    private readonly AppDbContext _context;

    public UpdateDriverCommand(IUnitOfWork uow, IDriverRepository drivers, AppDbContext context)
    {
        _uow = uow;
        _drivers = drivers;
        _context = context;
    }

    public async Task<DriverDto> Execute(Guid id, string firstName, string lastName, string documentNumber, string phoneNumber)
    {
        firstName = firstName.Trim();
        lastName = lastName.Trim();
        documentNumber = documentNumber.Trim();
        phoneNumber = phoneNumber.Trim();
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(documentNumber) || string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Nombres, apellidos, documento y teléfono son obligatorios.");
        if (documentNumber.Length > 20 || phoneNumber.Length > 20)
            throw new ArgumentException("Documento y teléfono no pueden exceder 20 caracteres.");

        var driver = await _drivers.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Conductor no encontrado.");
        var existing = await _drivers.GetByDocumentAsync(documentNumber);
        if (existing is not null && existing.Id != id)
            throw new BusinessConflictException("Ya existe otro conductor con ese documento en la empresa.");

        driver.Update(firstName, lastName, documentNumber, phoneNumber);
        await _drivers.UpdateAsync(driver);
        if (driver.UserId.HasValue)
        {
            var driverOwnerId = await _context.Drivers
                .Where(candidate => candidate.Id == id)
                .Select(candidate => candidate.OwnerUserId)
                .FirstOrDefaultAsync();
            var linkedUser = driverOwnerId.HasValue
                ? await _context.Users.FirstOrDefaultAsync(user =>
                    user.Id == driver.UserId.Value && user.OwnerUserId == driverOwnerId)
                : null;
            if (linkedUser is not null)
            {
                linkedUser.FirstName = firstName;
                linkedUser.LastName = lastName;
            }
        }
        await _uow.CommitAsync();
        return DriverApplicationMapper.ToDto(driver);
    }
}
