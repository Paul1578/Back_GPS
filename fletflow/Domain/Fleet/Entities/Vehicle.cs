namespace fletflow.Domain.Fleet.Entities
{
    public class Vehicle
    {
        public Guid Id { get; private set; }
        public string Plate { get; private set; }  = default!;    // Placa
        public string Brand { get; private set; }  = default!;    // Marca
        public string Model { get; private set; }  = default!;    // Modelo
        public int Year { get; private set; }          // Año
        public string? Description { get; private set; }
        public bool IsActive { get; private set; }

        private Vehicle() { }

        private Vehicle(Guid id, string plate, string brand, string model, int year, string? description)
        {
            Id = id;
            Plate = plate;
            Brand = brand;
            Model = model;
            Year = year;
            Description = description;
            IsActive = true;
        }

        /// <summary>
        /// Crear un vehículo nuevo (alta).
        /// </summary>
        public static Vehicle Create(string plate, string brand, string model, int year, string? description)
        {
            // Aquí puedes agregar reglas de dominio (validaciones de año, placa, etc.).
            return new Vehicle(Guid.NewGuid(), plate, brand, model, year, description);
        }

        /// <summary>
        /// Reconstruir un vehículo existente desde persistencia.
        /// </summary>
        public static Vehicle CreateExisting(Guid id, string plate, string brand, string model, int year, string? description, bool isActive)
        {
            var vehicle = new Vehicle(id, plate, brand, model, year, description);
            if (!isActive)
                vehicle.IsActive = false;

            return vehicle;
        }

        public void Update(string plate, string brand, string model, int year, string? description)
        {
            Plate = plate;
            Brand = brand;
            Model = model;
            Year = year;
            Description = description;
        }

        public void Activate() => IsActive = true;

        public void Deactivate() => IsActive = false;
    }
}
