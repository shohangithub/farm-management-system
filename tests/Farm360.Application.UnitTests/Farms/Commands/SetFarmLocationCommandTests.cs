using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Commands;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Farms.Commands;

/// <summary>The Weather page's "Set location": same ranges as the farm form, and no other farm field is touched.</summary>
public class SetFarmLocationCommandTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Mock<IFarmRepository> _repository = new();
    private readonly Mock<ITenantService> _tenant = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public SetFarmLocationCommandTests()
    {
        _tenant.Setup(t => t.TenantId).Returns(_tenantId);
    }

    [Theory]
    [InlineData(23.81, 90.41, true)]
    [InlineData(-90, -180, true)]
    [InlineData(90, 180, true)]
    [InlineData(90.0001, 90, false)]
    [InlineData(-91, 90, false)]
    [InlineData(23.8, 180.5, false)]
    [InlineData(23.8, -181, false)]
    public void Validator_ChecksTheSameRangesAsTheFarmForm(double lat, double lon, bool valid)
    {
        var result = new SetFarmLocationCommandValidator().Validate(new SetFarmLocationCommand(Guid.NewGuid(), lat, lon));

        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validator_NeedsAFarm()
    {
        new SetFarmLocationCommandValidator().Validate(new SetFarmLocationCommand(Guid.Empty, 23.8, 90.4))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SetsOnlyTheCoordinates()
    {
        var farm = Farm.Create(_tenantId, Guid.NewGuid(), "F-001", "Green Pasture", FarmType.Mixed,
            12.5, 10, null, null, "POLYGON", 80, "owner-1", "manager-1", "Riverside farm");
        _repository.Setup(r => r.GetByIdAsync(_tenantId, farm.Id, It.IsAny<CancellationToken>())).ReturnsAsync(farm);

        await Handler().Handle(new SetFarmLocationCommand(farm.Id, 23.810331, 90.412521), CancellationToken.None);

        farm.Latitude.Should().Be(23.810331);
        farm.Longitude.Should().Be(90.412521);
        farm.FarmName.Should().Be("Green Pasture");
        farm.Type.Should().Be(FarmType.Mixed);
        farm.FarmSize.Should().Be(12.5);
        farm.LandArea.Should().Be(10);
        farm.MapPolygon.Should().Be("POLYGON");
        farm.Capacity.Should().Be(80);
        farm.OwnerId.Should().Be("owner-1");
        farm.ManagerId.Should().Be("manager-1");
        farm.Description.Should().Be("Riverside farm");
        _repository.Verify(r => r.Update(farm), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownFarm_IsNotFound()
    {
        var act = () => Handler().Handle(new SetFarmLocationCommand(Guid.NewGuid(), 23.8, 90.4), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private SetFarmLocationCommandHandler Handler() => new(_repository.Object, _tenant.Object, _unitOfWork.Object);
}
