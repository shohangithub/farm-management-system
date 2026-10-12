using Farm360.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Auth.Commands;

/// <summary>Updates the signed-in user's own name and email. Phone is the login ID and stays read-only.</summary>
public sealed record UpdateMyProfileCommand(string FullName, string? Email) : IRequest;

public sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

internal sealed class UpdateMyProfileCommandHandler(
    ICurrentUserService currentUserService,
    IUserProfileService userProfileService) : IRequestHandler<UpdateMyProfileCommand>
{
    public async Task Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        await userProfileService.UpdateAsync(userId, request.FullName, request.Email, cancellationToken);
    }
}
