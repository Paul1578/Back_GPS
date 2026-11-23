namespace fletflow.Api.Contracts.Routes;

public sealed class AddRoutePointEvidenceRequest
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Description { get; set; }
}
