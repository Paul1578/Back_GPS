namespace fletflow.Api.Contracts.Drivers
{
    public class CreateDriverRequest
    {
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string DocumentNumber { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
    }
}
