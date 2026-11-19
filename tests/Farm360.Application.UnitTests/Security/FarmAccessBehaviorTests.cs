using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Security;

public sealed class FarmAccessBehaviorTests
{
    internal sealed record FarmScopedRequest(Guid FarmId) : IRequest<string>;
    internal sealed record OptionalFarmRequest(Guid? FarmId) : IRequest<string>;
    internal sealed record UnscopedRequest(string Name) : IRequest<string>;

    private readonly Mock<IFarmAccessService> _farmAccess = new();

    [Fact]
    public async Task InaccessibleFarm_IsForbidden_AndHandlerNotCalled()
    {
        var farmId = Guid.NewGuid();
        _farmAccess.Setup(f => f.CanAccessFarmAsync(farmId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var behavior = new FarmAccessBehavior<FarmScopedRequest, string>(_farmAccess.Object);
        var called = false;

        var act = () => behavior.Handle(new FarmScopedRequest(farmId), () => { called = true; return Task.FromResult("ok"); }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        called.Should().BeFalse();
    }

    [Fact]
    public async Task AccessibleFarm_CallsHandler()
    {
        var farmId = Guid.NewGuid();
        _farmAccess.Setup(f => f.CanAccessFarmAsync(farmId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var behavior = new FarmAccessBehavior<OptionalFarmRequest, string>(_farmAccess.Object);

        var result = await behavior.Handle(new OptionalFarmRequest(farmId), () => Task.FromResult("ok"), CancellationToken.None);

        result.Should().Be("ok");
    }

    [Fact]
    public async Task NullOrMissingFarmId_IsNotChecked()
    {
        var nullable = new FarmAccessBehavior<OptionalFarmRequest, string>(_farmAccess.Object);
        var unscoped = new FarmAccessBehavior<UnscopedRequest, string>(_farmAccess.Object);

        (await nullable.Handle(new OptionalFarmRequest(null), () => Task.FromResult("a"), CancellationToken.None)).Should().Be("a");
        (await unscoped.Handle(new UnscopedRequest("x"), () => Task.FromResult("b"), CancellationToken.None)).Should().Be("b");

        _farmAccess.Verify(f => f.CanAccessFarmAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
