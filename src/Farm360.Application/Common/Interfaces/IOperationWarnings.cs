using System.Collections.Generic;

namespace Farm360.Application.Common.Interfaces;

/// <summary>
/// Non-blocking warnings raised while a request is handled (e.g. "Bank has ৳40,000; this spends
/// ৳60,000"). The work still succeeds; the API returns the messages in the
/// <c>X-Farm360-Warnings</c> response header so the client can show them.
/// </summary>
public interface IOperationWarnings
{
    IReadOnlyList<string> Messages { get; }

    void Add(string message);
}

/// <summary>Request-scoped collector behind <see cref="IOperationWarnings"/>.</summary>
public sealed class OperationWarnings : IOperationWarnings
{
    private readonly List<string> _messages = [];

    public IReadOnlyList<string> Messages => _messages;

    public void Add(string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && !_messages.Contains(message))
            _messages.Add(message);
    }
}
