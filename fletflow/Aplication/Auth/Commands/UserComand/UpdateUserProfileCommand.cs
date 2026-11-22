using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Application.Users.Commands;

public sealed class UpdateUserProfileCommand
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserProfileCommand(AppDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task Execute(Guid userId, string username, string firstName, string lastName)
    {
        var ownerUserId = _context.CurrentOwnerUserId
            ?? throw new InvalidOperationException("No se puede editar un perfil sin propietario autenticado.");
        var normalizedUsername = username.Trim();
        var normalizedFirstName = firstName.Trim();
        var normalizedLastName = lastName.Trim();

        var user = await _context.Users.FirstOrDefaultAsync(candidate =>
                candidate.Id == userId && candidate.OwnerUserId == ownerUserId)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        user.Username = normalizedUsername;
        user.FirstName = normalizedFirstName;
        user.LastName = normalizedLastName;

        var driver = await _context.Drivers.FirstOrDefaultAsync(candidate =>
            candidate.UserId == userId && candidate.OwnerUserId == ownerUserId);
        if (driver is not null)
        {
            driver.FirstName = normalizedFirstName;
            driver.LastName = normalizedLastName;
        }

        await _unitOfWork.CommitAsync();
    }
}
