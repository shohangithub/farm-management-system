// Disable specific CA rules that conflict with the Guard Clause fluent design pattern
// CA1716: "Shared" conflicts with VB keyword — suppressed for this project
// CA1822: GuardClause methods must be instance methods (fluent chain "Guard.Against.Null()")
// CA1815: GuardClause is a readonly struct used as a value — equality not needed
// CA2225: implicit operator alternate method — handled by Result.Success<T> factory
// CA1000: static factory methods on generic types are the established .NET pattern (e.g., Task.FromResult)
// CA1805: IsDescending explicit default is intentional for clarity in API contracts
#pragma warning disable CA1716
#pragma warning disable CA1822
#pragma warning disable CA1815
#pragma warning disable CA2225
#pragma warning disable CA1000
#pragma warning disable CA1805

using System.Runtime.CompilerServices;

namespace Farm360.SharedKernel.Guards;

/// <summary>
/// Guard clause library for defensive programming.
/// Constitution §3: Validate inputs at boundaries. Throw for programming errors only.
/// Usage: Guard.Against.Null(value, nameof(value));
/// </summary>
public static class Guard
{
    public static GuardClause Against => new();
}

/// <summary>Provides guard clause methods.</summary>
public readonly struct GuardClause
{
    /// <summary>Throws <see cref="ArgumentNullException"/> if input is null.</summary>
    public T Null<T>(T? input, [CallerArgumentExpression(nameof(input))] string? paramName = null)
        where T : class
    {
        if (input is null)
        {
            throw new ArgumentNullException(paramName, $"Required value '{paramName}' was null.");
        }

        return input;
    }

    /// <summary>Throws if string is null or whitespace.</summary>
    public string NullOrWhiteSpace(string? input, [CallerArgumentExpression(nameof(input))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException($"Required string '{paramName}' was null or empty.", paramName);
        }

        return input;
    }

    /// <summary>Throws if Guid is empty.</summary>
    public Guid EmptyGuid(Guid input, [CallerArgumentExpression(nameof(input))] string? paramName = null)
    {
        if (input == Guid.Empty)
        {
            throw new ArgumentException($"Required Guid '{paramName}' was empty.", paramName);
        }

        return input;
    }

    /// <summary>Throws if value is negative.</summary>
    public T Negative<T>(T input, [CallerArgumentExpression(nameof(input))] string? paramName = null)
        where T : IComparable<T>
    {
        if (input.CompareTo(default) < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, input, $"'{paramName}' cannot be negative.");
        }

        return input;
    }

    /// <summary>Throws if value is zero or negative.</summary>
    public T NegativeOrZero<T>(T input, [CallerArgumentExpression(nameof(input))] string? paramName = null)
        where T : IComparable<T>
    {
        if (input.CompareTo(default) <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, input, $"'{paramName}' must be greater than zero.");
        }

        return input;
    }

    /// <summary>Throws if the collection is null or empty.</summary>
    public IEnumerable<T> NullOrEmpty<T>(IEnumerable<T>? input, [CallerArgumentExpression(nameof(input))] string? paramName = null)
    {
        if (input is null || !input.Any())
        {
            throw new ArgumentException($"Collection '{paramName}' cannot be null or empty.", paramName);
        }

        return input;
    }

    /// <summary>Throws if value falls outside the given range (inclusive).</summary>
    public T OutOfRange<T>(T input, T min, T max, [CallerArgumentExpression(nameof(input))] string? paramName = null)
        where T : IComparable<T>
    {
        if (input.CompareTo(min) < 0 || input.CompareTo(max) > 0)
        {
            throw new ArgumentOutOfRangeException(paramName, input, $"'{paramName}' must be between {min} and {max}.");
        }

        return input;
    }

    /// <summary>Throws if condition is true.</summary>
    public void InvalidInput(bool condition, string paramName, string message)
    {
        if (condition)
        {
            throw new ArgumentException(message, paramName);
        }
    }
}
