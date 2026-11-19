using System.Reflection;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using MediatR;

namespace Farm360.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior: per-farm access for any request that carries a farm id.
/// A request with a public <c>FarmId</c> property (Guid or Guid?) is only handled when the farm is
/// in the caller's tenant and in the caller's allowed farms; otherwise <see cref="ForbiddenAccessException"/>
/// (403). Complements the HTTP-level FarmAccessMiddleware, which covers {farmId} route/query values;
/// this covers farm ids sent in request bodies.
/// </summary>
public sealed class FarmAccessBehavior<TRequest, TResponse>(IFarmAccessService farmAccess)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Resolved once per closed request type.
    private static readonly PropertyInfo? FarmIdProperty = FindFarmIdProperty(typeof(TRequest));

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (FarmIdProperty?.GetValue(request) is Guid farmId && farmId != Guid.Empty
            && !await farmAccess.CanAccessFarmAsync(farmId, cancellationToken))
        {
            throw new ForbiddenAccessException("You do not have access to this farm.");
        }

        return await next();
    }

    internal static PropertyInfo? FindFarmIdProperty(Type type)
    {
        var property = type.GetProperty("FarmId", BindingFlags.Public | BindingFlags.Instance);
        return property is not null && property.CanRead &&
               (property.PropertyType == typeof(Guid) || property.PropertyType == typeof(Guid?))
            ? property
            : null;
    }
}
