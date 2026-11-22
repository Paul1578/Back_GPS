namespace fletflow.Domain.Fleet.Entities;

public sealed record RoutePointEvidence(
    string Type,
    string Content,
    string? Description,
    DateTime CreatedAtUtc)
{
    public static RoutePointEvidence Create(string type, string content, string? description)
    {
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("El tipo y el contenido de evidencia son obligatorios.");

        type = type.Trim().ToLowerInvariant();
        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (description?.Length > 250)
            throw new ArgumentException("La descripción de evidencia no puede exceder 250 caracteres.", nameof(description));

        if (type == "note")
        {
            content = content.Trim();
            if (content.Length is 0 or > 2000)
                throw new ArgumentException("La nota debe tener entre 1 y 2000 caracteres.", nameof(content));
        }
        else if (type == "image")
        {
            const int maxDataUrlLength = 5_600_000;
            var allowedPrefix = new[]
            {
                "data:image/jpeg;base64,",
                "data:image/png;base64,",
                "data:image/webp;base64,"
            };
            if (content.Length > maxDataUrlLength || !allowedPrefix.Any(prefix => content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("La imagen debe ser JPEG, PNG o WebP y pesar como máximo 4 MB.", nameof(content));

            var separator = content.IndexOf(',');
            try
            {
                Convert.FromBase64String(content[(separator + 1)..]);
            }
            catch (FormatException)
            {
                throw new ArgumentException("El contenido de la imagen no es base64 válido.", nameof(content));
            }
        }
        else
        {
            throw new ArgumentException("El tipo de evidencia debe ser image o note.", nameof(type));
        }

        return new RoutePointEvidence(type, content, description, DateTime.UtcNow);
    }
}
