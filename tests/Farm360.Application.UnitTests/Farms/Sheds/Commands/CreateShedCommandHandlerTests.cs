using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Sheds.Commands;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Farms.Sheds.Commands;

public class CreateShedCommandHandlerTests
{
    private readonly Mock<IShedRepository> _repositoryMock;
    private readonly Mock<IFarmRepository> _farmRepositoryMock;
    private readonly Mock<ITenantService> _tenantServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateShedCommandHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _farmId = Guid.NewGuid();

    public CreateShedCommandHandlerTests()
    {
        _repositoryMock = new Mock<IShedRepository>();
        _farmRepositoryMock = new Mock<IFarmRepository>();
        _tenantServiceMock = new Mock<ITenantService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _tenantServiceMock.Setup(x => x.TenantId).Returns(_tenantId);

        // Dummy farm
        var farm = Farm.Create(_tenantId, Guid.NewGuid(), "F-001", "Main Farm", FarmType.Dairy, null, null, null, null, null, null, null, null, null);
        _farmRepositoryMock.Setup(x => x.GetByIdAsync(_tenantId, _farmId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(farm);

        _handler = new CreateShedCommandHandler(
            _repositoryMock.Object,
            _farmRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Create_Shed_When_Valid()
    {
        // Arrange
        var command = new CreateShedCommand(
            _farmId, "S-01", "North Shed", 100, "Broiler", "Concrete", "Tin", true, true, true);

        _repositoryMock.Setup(x => x.ExistsByNumberAsync(_tenantId, command.FarmId, command.ShedNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _repositoryMock.Verify(x => x.Add(It.IsAny<Shed>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Throw_ValidationException_When_FarmNotFound()
    {
        // Arrange
        var command = new CreateShedCommand(
            Guid.NewGuid(), "S-01", "North Shed", 100, "Broiler", "Concrete", "Tin", true, true, true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_ValidationException_When_ShedNumberExists()
    {
        // Arrange
        var command = new CreateShedCommand(
            _farmId, "S-01", "North Shed", 100, "Broiler", "Concrete", "Tin", true, true, true);

        _repositoryMock.Setup(x => x.ExistsByNumberAsync(_tenantId, command.FarmId, command.ShedNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
