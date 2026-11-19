using Farm360.Domain.Common;

namespace Farm360.Domain.Identity;

/// <summary>
/// An invitation for a person (by phone and/or email) to join a tenant with a role and an optional
/// branch. Only a SHA-256 hash of the accept token is stored; the raw token travels in the link.
/// Single-use: accepting, revoking or re-issuing invalidates the previous token.
/// </summary>
public sealed class Invitation : AuditableEntity
{
    /// <summary>How long an invitation link stays valid after it is (re)sent.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation() { }

    private Invitation(Guid id, Guid tenantId) : base(id, tenantId) { }

    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? BranchId { get; private set; }

    /// <summary>Lower-case hex SHA-256 of the raw accept token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }
    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;
    public Guid InvitedByUserId { get; private set; }
    public DateTime LastSentAtUtc { get; private set; }
    public int SendCount { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public static Invitation Create(
        Guid tenantId,
        string? phone,
        string? email,
        Guid roleId,
        Guid? branchId,
        Guid invitedByUserId,
        string tokenHash,
        DateTime nowUtc)
    {
        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

        if (phone is null && email is null)
            throw new ArgumentException("A phone number or an email address is required.");
        if (roleId == Guid.Empty) throw new ArgumentException("RoleId is required.", nameof(roleId));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new Invitation(Guid.NewGuid(), tenantId)
        {
            Phone = phone,
            Email = email,
            RoleId = roleId,
            BranchId = branchId,
            InvitedByUserId = invitedByUserId,
            TokenHash = tokenHash,
            ExpiresAtUtc = nowUtc.Add(Lifetime),
            LastSentAtUtc = nowUtc,
            SendCount = 1,
            Status = InvitationStatus.Pending
        };
    }

    /// <summary>Pending and not yet expired: the token can still be accepted.</summary>
    public bool IsUsable(DateTime nowUtc) => Status == InvitationStatus.Pending && nowUtc < ExpiresAtUtc;

    /// <summary>The status to show: a pending invitation past its expiry reads as Expired.</summary>
    public InvitationStatus EffectiveStatus(DateTime nowUtc) =>
        Status == InvitationStatus.Pending && nowUtc >= ExpiresAtUtc ? InvitationStatus.Expired : Status;

    public void Accept(Guid userId, DateTime nowUtc)
    {
        if (!IsUsable(nowUtc))
            throw new InvalidOperationException("This invitation is no longer valid.");
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));

        Status = InvitationStatus.Accepted;
        AcceptedAtUtc = nowUtc;
        AcceptedByUserId = userId;
    }

    public void Revoke(DateTime nowUtc)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be revoked.");
        Status = InvitationStatus.Revoked;
        RevokedAtUtc = nowUtc;
    }

    /// <summary>Replaces the token (the old link stops working) and restarts the 7-day validity.</summary>
    public void Reissue(string tokenHash, DateTime nowUtc)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be resent.");
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        TokenHash = tokenHash;
        ExpiresAtUtc = nowUtc.Add(Lifetime);
        LastSentAtUtc = nowUtc;
        SendCount++;
    }
}

public enum InvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Revoked = 3,
    Expired = 4
}
