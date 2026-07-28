namespace Farm360.Domain.Feeding.Exceptions;

public class FeedingDomainException : Exception
{
    public FeedingDomainException(string message) : base(message)
    {
    }

    public FeedingDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
