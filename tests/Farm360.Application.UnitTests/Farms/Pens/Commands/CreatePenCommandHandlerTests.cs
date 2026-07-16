using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Pens.Commands;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Farms.Pens.Commands;

public class CreatePenCommandHandlerTests
{
    private readonly Mock<IPenRepository> _repositoryMock;
    private readonly Mock<IShedRepository> _shedRepositoryMock;
    private readonly Mock<ITenantService> _tenantServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreatePenCommandHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _shedId = Guid.NewGuid();

    public CreatePenCommandHandlerTests()
    {
        _repositoryMock = new Mock<IPenRepository>();
        _shedRepositoryMock = new Mock<IShedRepository>();
        _tenantServiceMock = new Mock<ITenantService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _tenantServiceMock.Setup(x => x.TenantId).Returns(_tenantId);

        // Dummy shed
        var shed = Shed.Create(_tenantId, Guid.NewGuid(), "S-001", "Main Shed", 100, "Cows", "Dirt", "Tin", false, false, false);
        _shedRepositoryMock.Setup(x => x.GetByIdAsync(_tenantId, _shedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shed);

        _handler = new CreatePenCommandHandler(
            _repositoryMock.Object,
            _shedRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Create_Pen_When_Valid()
    {
        // Arrange
        var command = new CreatePenCommand(
            _shedId, "P-01", "Nursery Pen A", 10, "Weaners", "Needs cleaning");

        _repositoryMock.Setup(x => x.ExistsByNumberAsync(_tenantId, command.ShedId, command.PenNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _repositoryMock.Verify(x => x.Add(It.IsAny<Pen>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Throw_ValidationException_When_ShedNotFound()
    {
        // Arrange
        var command = new CreatePenCommand(
            Guid.NewGuid(), "P-01", "Nursery Pen A", 10, "Weaners", "Needs cleaning");

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_ValidationException_When_PenNumberExists()
    {
        // Arrange
        var command = new CreatePenCommand(
            _shedId, "P-01", "Nursery Pen A", 10, "Weaners", "Needs cleaning");

        _repositoryMock.Setup(x => x.ExistsByNumberAsync(_tenantId, command.ShedId, command.PenNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
