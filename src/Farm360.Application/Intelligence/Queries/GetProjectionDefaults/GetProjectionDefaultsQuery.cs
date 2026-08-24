using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Services;
using Farm360.Contracts.Intelligence;
using MediatR;

namespace Farm360.Application.Intelligence.Queries.GetProjectionDefaults;

public sealed record GetProjectionDefaultsQuery(Guid AnimalId) : IRequest<ProjectionDefaultsDto>;

public sealed class GetProjectionDefaultsQueryHandler : IRequestHandler<GetProjectionDefaultsQuery, ProjectionDefaultsDto>
{
    private readonly IProjectionDefaultsResolver _resolver;

    public GetProjectionDefaultsQueryHandler(IProjectionDefaultsResolver resolver)
    {
        _resolver = resolver;
    }

    public Task<ProjectionDefaultsDto> Handle(GetProjectionDefaultsQuery request, CancellationToken cancellationToken)
    {
        return _resolver.ResolveDefaultsAsync(request.AnimalId, cancellationToken);
    }
}
