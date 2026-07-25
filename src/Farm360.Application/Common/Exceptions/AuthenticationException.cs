using System;

namespace Farm360.Application.Common.Exceptions;

public class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message)
    {
    }
}
