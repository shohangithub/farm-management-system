using Farm360.Application.Common.Interfaces;
using Farm360.Application.Organizations.Commands;
using Farm360.Domain.Organizations;
using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.Repositories;

using FluentAssertions;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Organizations.Commands;

public class CreateOrganizationCommandHandlerTests
{
    private readonly Mock<IOrganizationRepository> _repositoryMock;
    private readonly Mock<ITenantService> _tenantServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateOrganizationCommandHandler _handler;

    public CreateOrganizationCommandHandlerTests()
    {
        _repositoryMock = new Mock<IOrganizationRepository>();
        _tenantServiceMock = new Mock<ITenantService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _tenantServiceMock.Setup(x => x.TenantId).Returns(Guid.NewGuid());

        _unitOfWorkMock.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<ITransaction>());

        _handler = new CreateOrganizationCommandHandler(
            _repositoryMock.Object,
            _tenantServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldReturnOrganizationId()
    {
        // Arrange
        var command = new CreateOrganizationCommand(
            "Test Org",
            null,
            "test@org.com",
            null,
            null,
            null,
            null,
            "USD",
            "UTC",
            "en-US",
            null,
            null,
            null,
            null,
            null,
            BusinessType.LLC);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        _repositoryMock.Verify(x => x.Add(It.Is<Organization>(o => o.Name == "Test Org")), Times.Once);
        _unitOfWorkMock.Verify(x => x.CommitTransactionAsync(It.IsAny<ITransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
