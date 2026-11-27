using Farm360.Domain.Common;

namespace Farm360.Domain.Tenancy;

/// <summary>
/// Notification type discriminator.
/// </summary>
public enum NotificationType
{
    Info = 1,
    Success = 2,
    Warning = 3,
    Alert = 4,
    SystemAnnouncement = 5
}

/// <summary>
/// In-app notification for a specific user within a tenant.
/// Delivered in real-time via SignalR. Persisted for offline retrieval.
/// F360-MTA-2026-001: Scoped by TenantId via Global Query Filter (AuditableEntity).
/// </summary>
public sealed class Notification : AuditableEntity
{
    private Notification() { }

    private Notification(Guid id, Guid tenantId, Guid userId, NotificationType type,
        string title, string body, string? data) : base(id, tenantId)
    {
        UserId = userId;
        Type = type;
        Title = title;
        Body = body;
        Data = data;
        IsRead = false;
    }

    /// <summary>Target user. Must belong to this tenant.</summary>
    public Guid UserId { get; private set; }

    public NotificationType Type { get; private set; }

    /// <summary>Short notification header (max 100 chars).</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Notification body text (max 500 chars).</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>Optional JSON payload for deep-link or action context.</summary>
    public string? Data { get; private set; }

    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    /// <summary>The farm it is about (health reminders); null for tenant-wide notices.</summary>
    public Guid? FarmId { get; private set; }

    /// <summary>What produced it, e.g. "health.today", "health.tomorrow", "health.overdue".</summary>
    public string? Category { get; private set; }

    /// <summary>The in-app page it opens, e.g. "/health/today?focus=overdue".</summary>
    public string? Link { get; private set; }

    /// <summary>
    /// Keeps a reminder from being created twice for the same user (one per farm, kind and day). Null
    /// for one-off notifications.
    /// </summary>
    public string? DedupKey { get; private set; }

    /// <summary>
    /// A translatable reminder: <paramref name="titleKey"/> and <paramref name="bodyKey"/> are the English
    /// texts the web dictionary translates, with <c>{name}</c> placeholders filled from
    /// <paramref name="paramsJson"/> (stored in <see cref="Data"/>), so each user reads it in their language.
    /// </summary>
    public static Notification CreateReminder(
        Guid tenantId,
        Guid userId,
        Guid? farmId,
        NotificationType type,
        string category,
        string titleKey,
        string bodyKey,
        string? paramsJson,
        string? link,
        string? dedupKey)
    {
        var notification = Create(tenantId, userId, type, titleKey, bodyKey, paramsJson);
        notification.FarmId = farmId;
        notification.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        notification.Link = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
        notification.DedupKey = string.IsNullOrWhiteSpace(dedupKey) ? null : dedupKey.Trim();
        return notification;
    }

    // ── Factory ──────────────────────────────────────────────────────────────
    public static Notification Create(
        Guid tenantId,
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));

        return new Notification(Guid.NewGuid(), tenantId, userId, type, title.Trim(), body.Trim(), data);
    }

    // ── Business methods ─────────────────────────────────────────────────────
    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
