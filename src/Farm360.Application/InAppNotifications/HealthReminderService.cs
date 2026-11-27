using System.Text.Json;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Jobs;
using Farm360.Application.Health.Tasks;
using Farm360.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.InAppNotifications;

/// <summary>Stored in-app notifications (the header bell). Implemented in Persistence; tenant-scoped.</summary>
public interface INotificationStore
{
    Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Notification?> GetForUserByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The (user, key) pairs among these that already have a notification.</summary>
    Task<IReadOnlySet<(Guid UserId, string DedupKey)>> GetExistingKeysAsync(
        IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<string> dedupKeys, CancellationToken cancellationToken = default);

    void Add(Notification notification);
}

/// <summary>Who gets health reminders for a farm. Implemented in Persistence.</summary>
public interface IHealthReminderRecipients
{
    /// <summary>
    /// Active members of the tenant whose role does health/feeding work (Owner, Farm Manager,
    /// Veterinarian, Worker; not Viewer, not the demo visitor) and who can access the farm.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetUserIdsAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default);
}

/// <summary>A notification as the bell shows it: translatable title/body keys with their parameters.</summary>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    string? Category,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Params,
    string? Link,
    bool IsRead,
    DateTime CreatedAtUtc,
    Guid? FarmId)
{
    public const string LiveEventName = "NewNotification";

    public static NotificationDto From(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        IReadOnlyDictionary<string, string> parameters = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(notification.Data))
        {
            try
            {
                parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(notification.Data) ?? new Dictionary<string, string>();
            }
            catch (JsonException)
            {
                // Data of an older notification that is not a parameter map: show the keys as they are.
            }
        }

        return new NotificationDto(notification.Id, notification.Type.ToString(), notification.Category, notification.Title,
            notification.Body, parameters, notification.Link, notification.IsRead, notification.CreatedAtUtc, notification.FarmId);
    }
}

/// <summary>Creates the daily "today / tomorrow / overdue" health reminders for one farm.</summary>
public interface IHealthReminderService
{
    /// <summary>
    /// Creates (and pushes live) the reminders for <paramref name="today"/> on one farm: at most one per
    /// user, kind and day, so running it again the same day creates nothing.
    /// </summary>
    Task<IReadOnlyList<Notification>> GenerateForFarmAsync(Guid tenantId, Guid farmId, DateOnly today, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IHealthReminderService"/>
public sealed class HealthReminderService(
    IHealthTaskSource tasks,
    IHealthReminderRecipients recipients,
    INotificationStore store,
    IUnitOfWork unitOfWork,
    ILogger<HealthReminderService> logger,
    INotificationService? live = null) : IHealthReminderService
{
    public const string CategoryToday = "health.today";
    public const string CategoryTomorrow = "health.tomorrow";
    public const string CategoryOverdue = "health.overdue";

    /// <summary>Tags listed per task before "…".</summary>
    public const int TagsPerTask = 3;

    public async Task<IReadOnlyList<Notification>> GenerateForFarmAsync(Guid tenantId, Guid farmId, DateOnly today, CancellationToken cancellationToken = default)
    {
        var rows = await tasks.GetPendingAsync(farmId, today.AddDays(1), cancellationToken);
        if (rows.Count == 0)
            return [];

        var userIds = await recipients.GetUserIdsAsync(tenantId, farmId, cancellationToken);
        if (userIds.Count == 0)
            return [];

        var groups = rows
            .GroupBy(r => HealthTaskBuckets.For(r.Event.ScheduledDate, today))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<HealthTaskRow>)g.ToList());

        var keys = groups.Keys.Select(bucket => DedupKey(bucket, farmId, today)).ToList();
        var existing = await store.GetExistingKeysAsync(userIds, keys, cancellationToken);

        var created = new List<Notification>();
        foreach (var (bucket, items) in groups)
        {
            var key = DedupKey(bucket, farmId, today);
            var (category, type, title, body) = Texts(bucket, AnimalCount(items));
            var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["count"] = AnimalCount(items).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["items"] = Summarise(items),
            });

            foreach (var userId in userIds)
            {
                if (existing.Contains((userId, key)))
                    continue;

                var notification = Notification.CreateReminder(tenantId, userId, farmId, type, category, title, body,
                    parameters, $"/health/today?focus={bucket}", key);
                store.Add(notification);
                created.Add(notification);
            }
        }

        if (created.Count == 0)
            return [];

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrors.IsUniqueViolation(ex))
        {
            // Another run made today's reminders at the same moment: theirs stand.
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Health reminders for farm {FarmId} on {Day} were already created by a concurrent run.", farmId, today);
            return [];
        }

        if (live is not null)
        {
            foreach (var notification in created)
            {
#pragma warning disable CA1031 // A failed live push must not undo stored reminders; the bell loads them anyway.
                try
                {
                    await live.SendToUserAsync(tenantId, notification.UserId, NotificationDto.LiveEventName,
                        NotificationDto.From(notification), cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Live push of notification {NotificationId} failed.", notification.Id);
                }
#pragma warning restore CA1031
            }
        }

        return created;
    }

    public static string DedupKey(string bucket, Guid farmId, DateOnly day) =>
        $"health:{bucket}:{farmId:N}:{day:yyyyMMdd}";

    private static int AnimalCount(IReadOnlyList<HealthTaskRow> rows) => rows.Select(r => r.Event.AnimalId).Distinct().Count();

    /// <summary>"Dewormer (B-04, B-07, C-01…), FMD (C-02)" — tasks by how many animals need them.</summary>
    public static string Summarise(IEnumerable<HealthTaskRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var parts = rows
            .GroupBy(r => r.Event.VaccineName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Select(r => r.Event.AnimalId).Distinct().Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var tags = g.Select(r => r.AnimalTag).Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
                var shown = string.Join(", ", tags.Take(TagsPerTask));
                return $"{g.Key} ({shown}{(tags.Count > TagsPerTask ? "…" : string.Empty)})";
            })
            .ToList();

        var text = string.Join(", ", parts);
        return text.Length <= 400 ? text : text[..399] + "…";
    }

    private static (string Category, NotificationType Type, string Title, string Body) Texts(string bucket, int animals)
    {
        var one = animals == 1;
        return bucket switch
        {
            HealthTaskBuckets.Overdue => (CategoryOverdue, NotificationType.Warning, "Overdue health tasks",
                one ? "{count} animal has overdue tasks: {items}" : "{count} animals have overdue tasks: {items}"),
            HealthTaskBuckets.Today => (CategoryToday, NotificationType.Info, "Health tasks for today",
                one ? "{count} animal is due today: {items}" : "{count} animals are due today: {items}"),
            _ => (CategoryTomorrow, NotificationType.Info, "Health tasks for tomorrow",
                one ? "{count} animal is due tomorrow: {items}" : "{count} animals are due tomorrow: {items}"),
        };
    }
}
