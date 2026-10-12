using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Common.Interfaces;

public sealed record UserProfileDetails(string? FullName, string? Email, string? PhoneNumber);

/// <summary>
/// Reads and updates the signed-in user's own contact details. Implemented in the Identity layer,
/// which owns the user record.
/// </summary>
public interface IUserProfileService
{
    Task<UserProfileDetails> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates name and email. The phone number is deliberately not editable here: it is the login
    /// identifier, and changing it safely needs an OTP re-verification flow.
    /// </summary>
    Task UpdateAsync(Guid userId, string fullName, string? email, CancellationToken cancellationToken = default);
}
