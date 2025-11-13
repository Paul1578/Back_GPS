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
            string phoneNumber)
        {
            documentNumber = documentNumber.Trim();

            var existing = await _drivers.GetByDocumentAsync(documentNumber);
            if (existing is not null)
                throw new InvalidOperationException($"Ya existe un conductor con documento {documentNumber}.");

            var driver = Driver.Create(firstName, lastName, documentNumber, phoneNumber);

            await _drivers.AddAsync(driver);
            await _uow.CommitAsync();

            return DriverApplicationMapper.ToDto(driver);
        }
    }
}
