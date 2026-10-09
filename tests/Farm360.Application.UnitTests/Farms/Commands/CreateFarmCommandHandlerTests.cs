using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Commands;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Enums;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Farms.Commands;

public class CreateFarmCommandHandlerTests
{
    private readonly Mock<IFarmRepository> _repositoryMock;
    private readonly Mock<IBranchRepository> _branchRepositoryMock;
    private readonly Mock<ITenantRepository> _tenantRepositoryMock;
    private readonly Mock<ITenantService> _tenantServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateFarmCommandHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();

    public CreateFarmCommandHandlerTests()
    {
        _repositoryMock = new Mock<IFarmRepository>();
        _branchRepositoryMock = new Mock<IBranchRepository>();
        _tenantRepositoryMock = new Mock<ITenantRepository>();
        _tenantServiceMock = new Mock<ITenantService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _tenantServiceMock.Setup(x => x.TenantId).Returns(_tenantId);

        // Standard tier (MaxFarms: 5) with room to spare, so the quota check never interferes
        // with what these tests are actually exercising.
        var tenant = Tenant.Create("Test Farm", "test-farm", SubscriptionTier.Standard);
        _tenantRepositoryMock.Setup(x => x.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _tenantRepositoryMock.Setup(x => x.GetTenantUsageCountsAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, 0, 0));

        // Dummy branch
        var branch = Branch.Create(_tenantId, Guid.NewGuid(), "BR-001", "Main", "test@test.com", true);
        _branchRepositoryMock.Setup(x => x.GetByIdAsync(_tenantId, _branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branch);

        _handler = new CreateFarmCommandHandler(
            _repositoryMock.Object,
            _branchRepositoryMock.Object,
            _tenantRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Create_Farm_When_Valid()
    {
        // Arrange
        var command = new CreateFarmCommand(
            _branchId, "F-001", "Sunny Dairy", FarmType.Dairy, 10, 1000, 23.0, 90.0, null, 100, null, null, null);

        _repositoryMock.Setup(x => x.ExistsByCodeAsync(_tenantId, command.FarmCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _repositoryMock.Verify(x => x.Add(It.IsAny<Farm>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Throw_ValidationException_When_BranchNotFound()
    {
        // Arrange
        var command = new CreateFarmCommand(
            Guid.NewGuid(), "F-001", "Sunny Dairy", FarmType.Dairy, null, null, null, null, null, null, null, null, null);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_ValidationException_When_FarmCodeExists()
    {
        // Arrange
        var command = new CreateFarmCommand(
            _branchId, "F-001", "Sunny Dairy", FarmType.Dairy, null, null, null, null, null, null, null, null, null);

        _repositoryMock.Setup(x => x.ExistsByCodeAsync(_tenantId, command.FarmCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
