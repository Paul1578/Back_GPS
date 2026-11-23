namespace fletflow.Domain.Fleet.Exceptions;

public sealed class BusinessConflictException : InvalidOperationException
{
    public BusinessConflictException(string message) : base(message)
    {
    }
}
