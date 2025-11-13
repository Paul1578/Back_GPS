namespace fletflow.Domain.Fleet.Entities
{
    public class Driver
    {
        public Guid Id { get; private set; }
        public string FirstName { get; private set; } = default!;
        public string LastName { get; private set; } = default!;
        public string DocumentNumber { get; private set; } = default!; // cédula / id
        public string PhoneNumber { get; private set; } = default!;
        public bool IsActive { get; private set; }
        public Guid? VehicleId { get; private set; }

        private Driver() { }

        private Driver(Guid id, string firstName, string lastName, string documentNumber, string phoneNumber, Guid? vehicleId = null)
        {
            Id = id;
            FirstName = firstName;
            LastName = lastName;
            DocumentNumber = documentNumber;
            PhoneNumber = phoneNumber;
            VehicleId = vehicleId;
            IsActive = true;
        }

        public static Driver Create(string firstName, string lastName, string documentNumber, string phoneNumber)
        {
            return new Driver(
                Guid.NewGuid(),
                firstName.Trim(),
                lastName.Trim(),
                documentNumber.Trim(),
                phoneNumber.Trim()
            );
        }

        public static Driver CreateExisting(Guid id, string firstName, string lastName, string documentNumber, string phoneNumber, bool isActive, Guid? vehicleId)
        {
            var d = new Driver(id, firstName, lastName, documentNumber, phoneNumber, vehicleId);
            if (!isActive) d.IsActive = false;
            return d;
        }

        public void Update(string firstName, string lastName, string documentNumber, string phoneNumber)
        {
            FirstName = firstName.Trim();
            LastName = lastName.Trim();
            DocumentNumber = documentNumber.Trim();
            PhoneNumber = phoneNumber.Trim();
        }

        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;

        public void AssignToVehicle(Guid vehicleId)
        {
            VehicleId = vehicleId;
        }
        
        public void UnassignVehicle()
        {
            VehicleId = null;
        }

    }
}
