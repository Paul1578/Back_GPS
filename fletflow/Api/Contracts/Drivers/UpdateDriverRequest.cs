namespace fletflow.Api.Contracts.Drivers;

public class UpdateDriverRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
