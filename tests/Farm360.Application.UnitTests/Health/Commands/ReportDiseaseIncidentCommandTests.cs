using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.Commands.DiseaseIncidents;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Health.Enums;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Health.Commands;

public class ReportDiseaseIncidentCommandTests
{
    private readonly Mock<IDiseaseIncidentRepository> _incidentRepositoryMock;
    private readonly Mock<ITenantService> _tenantServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITransaction> _transactionMock;

    public ReportDiseaseIncidentCommandTests()
    {
        _incidentRepositoryMock = new Mock<IDiseaseIncidentRepository>();
        _tenantServiceMock = new Mock<ITenantService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _transactionMock = new Mock<ITransaction>();

        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transactionMock.Object);

        _tenantServiceMock.Setup(t => t.TenantId).Returns(Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_ValidRequest_ShouldReportIncident()
    {
        // Arrange
        var command = new ReportDiseaseIncidentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "FMD Outbreak",
            IncidentSeverity.Severe,
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Blisters on mouth and hooves",
            12,
            "Quarantine initiated"
        );

        var handler = new ReportDiseaseIncidentCommandHandler(
            _incidentRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _incidentRepositoryMock.Verify(r => r.Add(It.Is<Domain.Health.DiseaseIncident>(i => i.DiseaseName == "FMD Outbreak" && i.AffectedAnimalCount == 12)), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
