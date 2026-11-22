namespace fletflow.Application.Fleet.Commands;

internal static class VehicleCommandInput
{
    public static string Required(string? value, string field, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException($"{field} es obligatorio.", field);
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{field} no puede exceder {maxLength} caracteres.", field);
        return normalized;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{field} no puede exceder {maxLength} caracteres.", field);
        return normalized;
    }

    public static int Year(int year)
    {
        if (year is < 1900 or > 2100)
            throw new ArgumentOutOfRangeException(nameof(year), "El año debe estar entre 1900 y 2100.");
        return year;
    }
}
