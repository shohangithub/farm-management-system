using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Organizations.Branches.Commands;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Repositories;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Organizations.Branches.Commands;

public class CreateBranchCommandHandlerTests
{
    private readonly Mock<IBranchRepository> _repositoryMock;
    private readonly Mock<IOrganizationRepository> _orgRepositoryMock;
    private readonly Mock<ITenantService> _tenantServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateBranchCommandHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public CreateBranchCommandHandlerTests()
    {
        _repositoryMock = new Mock<IBranchRepository>();
        _orgRepositoryMock = new Mock<IOrganizationRepository>();
        _tenantServiceMock = new Mock<ITenantService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _tenantServiceMock.Setup(x => x.TenantId).Returns(_tenantId);

        var org = Organization.Create(
            _tenantId, 
            "Test Org", 
            null, 
            "test@test.com", 
            null, 
            null, 
            null, 
            null, 
            "USD", 
            "UTC", 
            "en-US", 
            null, 
            (Farm360.Domain.Organizations.Enums.BusinessType)1);
        _orgRepositoryMock.Setup(x => x.GetByIdAsync(_orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(org);

        _handler = new CreateBranchCommandHandler(
            _repositoryMock.Object,
            _orgRepositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_Should_Create_Branch_When_Valid()
    {
        // Arrange
        var command = new CreateBranchCommand(
            _orgId, "BR-001", "Main Branch", "br@test.com", null, null, null, null, null, null, null, null, null, null, true);

        _repositoryMock.Setup(x => x.ExistsByCodeAsync(_tenantId, command.BranchCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result);
        _repositoryMock.Verify(x => x.Add(It.IsAny<Branch>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Throw_ValidationException_When_OrganizationNotFound()
    {
        // Arrange
        var command = new CreateBranchCommand(
            Guid.NewGuid(), "BR-001", "Main Branch", "br@test.com", null, null, null, null, null, null, null, null, null, null, true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Should_Throw_ValidationException_When_BranchCodeExists()
    {
        // Arrange
        var command = new CreateBranchCommand(
            _orgId, "BR-001", "Main Branch", "br@test.com", null, null, null, null, null, null, null, null, null, null, true);

        _repositoryMock.Setup(x => x.ExistsByCodeAsync(_tenantId, command.BranchCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _handler.Handle(command, CancellationToken.None));
    }
}
