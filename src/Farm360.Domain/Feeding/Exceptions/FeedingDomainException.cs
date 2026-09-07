using Farm360.Domain.Exceptions;

namespace Farm360.Domain.Feeding.Exceptions;

public class FeedingDomainException : DomainException
{
    public FeedingDomainException(string message) : base(message)
    {
    }

    public FeedingDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
