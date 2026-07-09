using Farm360.Application.Common.Exceptions;
using FluentValidation;
using MediatR;
using ValidationException = Farm360.Application.Common.Exceptions.ValidationException;

namespace Farm360.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior: FluentValidation enforcement.
/// Constitution §9 (Validation Standards): All commands validated before handler execution.
/// Aggregates ALL validation errors before returning — no fail-fast.
/// Runs SECOND in the pipeline (after logging, before transaction).
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            // MediatR 12: delegate takes no CancellationToken
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
