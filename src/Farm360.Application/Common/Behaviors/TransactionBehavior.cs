using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Common.Behaviors;

/// <summary>
/// Marker interface for commands that require a database transaction.
/// Apply to commands that perform multiple write operations that must be atomic.
/// Constitution §8 (CQRS): Commands with side effects use transactions.
/// Queries NEVER implement this interface.
/// CA1040 suppressed: marker interfaces are an established pattern for pipeline behavior routing.
/// </summary>
#pragma warning disable CA1040
public interface ITransactionalCommand { }
#pragma warning restore CA1040

/// <summary>
/// MediatR pipeline behavior: wraps ITransactionalCommand handlers in a DB transaction.
/// Runs FOURTH in the pipeline (after validation, before caching).
/// If the handler throws, the transaction is rolled back automatically.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only transactional commands get a transaction wrapper
        if (request is not ITransactionalCommand)
        {
            return await next();
        }

        var requestName = typeof(TRequest).Name;

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Farm360 Transaction: Begin for {RequestName}", requestName);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // MediatR 12: delegate takes no CancellationToken
            var response = await next();

            await unitOfWork.CommitTransactionAsync(transaction, cancellationToken);

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("Farm360 Transaction: Committed for {RequestName}", requestName);
            }

            return response;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync(transaction, cancellationToken);

            logger.LogError(ex, "Farm360 Transaction: Rolled back for {RequestName}", requestName);

            throw;
        }
    }
}
