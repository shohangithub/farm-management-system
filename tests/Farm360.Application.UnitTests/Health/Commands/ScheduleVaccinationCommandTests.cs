using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.Commands.VaccinationEvents;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Health.Commands;

public class ScheduleVaccinationCommandTests
{
    private readonly Mock<IVaccinationRepository> _vaccinationRepositoryMock;
    private readonly Mock<IAnimalRepository> _animalRepositoryMock;
    private readonly Mock<ITenantService> _tenantServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITransaction> _transactionMock;

    public ScheduleVaccinationCommandTests()
    {
        _vaccinationRepositoryMock = new Mock<IVaccinationRepository>();
        _animalRepositoryMock = new Mock<IAnimalRepository>();
        _tenantServiceMock = new Mock<ITenantService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<ITransaction>();

        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _tenantServiceMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldScheduleVaccination()
    {
        // Arrange
        var animalId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var tag = AnimalTag.Create("BD-1234", TagType.Manual);
        var animal = Animal.Create(_tenantServiceMock.Object.TenantId, farmId, null, tag, AnimalSpecies.CattleBeef, "Local", AnimalSex.Male, DateOnly.FromDateTime(DateTime.UtcNow), AcquisitionType.Purchased, DateOnly.FromDateTime(DateTime.UtcNow), null, null);
        
        _animalRepositoryMock.Setup(r => r.GetByIdAsync(animalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(animal);

        var command = new ScheduleVaccinationCommand(
            animalId,
            null,
            "FMD",
            "B-001",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            "Notes"
        );

        var handler = new ScheduleVaccinationCommandHandler(
            _vaccinationRepositoryMock.Object,
            _animalRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _vaccinationRepositoryMock.Verify(r => r.AddEvent(It.Is<Domain.Health.VaccinationEvent>(e => e.VaccineName == "FMD")), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(_transactionMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AnimalNotFound_ShouldThrowException()
    {
        // Arrange
        var command = new ScheduleVaccinationCommand(
            Guid.NewGuid(),
            null,
            "FMD",
            "B-001",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            null
        );

        _animalRepositoryMock.Setup(r => r.GetByIdAsync(command.AnimalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Animal?)null);

        var handler = new ScheduleVaccinationCommandHandler(
            _vaccinationRepositoryMock.Object,
            _animalRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }
}
