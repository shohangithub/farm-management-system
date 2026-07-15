using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.Commands.MedicalTreatments;
using Farm360.Domain.Health.Exceptions;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Health.Commands;

public class LogMedicalTreatmentCommandTests
{
    private readonly Mock<IMedicalTreatmentRepository> _medicalTreatmentRepositoryMock;
    private readonly Mock<IAnimalRepository> _animalRepositoryMock;
    private readonly Mock<ITenantService> _tenantServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITransaction> _transactionMock;

    public LogMedicalTreatmentCommandTests()
    {
        _medicalTreatmentRepositoryMock = new Mock<IMedicalTreatmentRepository>();
        _animalRepositoryMock = new Mock<IAnimalRepository>();
        _tenantServiceMock = new Mock<ITenantService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<ITransaction>();

        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _tenantServiceMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldLogTreatment()
    {
        // Arrange
        var animalId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var tag = AnimalTag.Create("BD-1234", TagType.Manual);
        var animal = Animal.Create(_tenantServiceMock.Object.TenantId, farmId, null, tag, AnimalSpecies.CattleBeef, "Local", AnimalSex.Male, DateOnly.FromDateTime(DateTime.UtcNow), AcquisitionType.Purchased, DateOnly.FromDateTime(DateTime.UtcNow), null, null);
        
        _animalRepositoryMock.Setup(r => r.GetByIdAsync(animalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(animal);

        _medicalTreatmentRepositoryMock.Setup(r => r.HasActiveTreatmentForMedicationAsync(animalId, "Oxy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new LogMedicalTreatmentCommand(
            animalId,
            "Fever",
            "Oxy",
            10,
            "ml",
            5,
            14,
            DateOnly.FromDateTime(DateTime.UtcNow),
            1200,
            "Dr. Smith",
            "Notes"
        );

        var handler = new LogMedicalTreatmentCommandHandler(
            _medicalTreatmentRepositoryMock.Object,
            _animalRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _medicalTreatmentRepositoryMock.Verify(r => r.Add(It.Is<Domain.Health.MedicalTreatment>(t => t.MedicationName == "Oxy")), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(_transactionMock.Object, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OverlappingTreatment_ShouldThrowException()
    {
        // Arrange
        var animalId = Guid.NewGuid();
        var farmId = Guid.NewGuid();
        var tag = AnimalTag.Create("BD-1234", TagType.Manual);
        var animal = Animal.Create(_tenantServiceMock.Object.TenantId, farmId, null, tag, AnimalSpecies.CattleBeef, "Local", AnimalSex.Male, DateOnly.FromDateTime(DateTime.UtcNow), AcquisitionType.Purchased, DateOnly.FromDateTime(DateTime.UtcNow), null, null);
        
        _animalRepositoryMock.Setup(r => r.GetByIdAsync(animalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(animal);

        _medicalTreatmentRepositoryMock.Setup(r => r.HasActiveTreatmentForMedicationAsync(animalId, "Oxy", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new LogMedicalTreatmentCommand(
            animalId,
            "Fever",
            "Oxy",
            10,
            "ml",
            5,
            14,
            DateOnly.FromDateTime(DateTime.UtcNow),
            1200,
            "Dr. Smith",
            "Notes"
        );

        var handler = new LogMedicalTreatmentCommandHandler(
            _medicalTreatmentRepositoryMock.Object,
            _animalRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<OverlappingTreatmentException>(() => handler.Handle(command, CancellationToken.None));
    }
}
