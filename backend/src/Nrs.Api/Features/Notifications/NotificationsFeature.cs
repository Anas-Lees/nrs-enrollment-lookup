using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Notifications;

/// <summary>
/// Vertical slice: the staff notification bell. Notifications are written by the enrollment
/// review workflow (see EnrollmentProcessWorker) addressed either to a username (the operator
/// who submitted an application) or to a role (reviewers, supervisors); a user sees the union
/// of both. Bilingual bodies — the SPA picks the language.
/// </summary>
public static class NotificationsFeature
{
    public record NotificationDto
    {
        public Guid Id { get; init; }

        public string Kind { get; init; } = null!;

        public Guid? EnrollmentId { get; init; }

        public string? ReferenceNumber { get; init; }

        public string MessageEn { get; init; } = null!;

        public string MessageAr { get; init; } = null!;

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset? ReadAtUtc { get; init; }
    }

    public record NotificationListDto
    {
        public IReadOnlyList<NotificationDto> Items { get; init; } = [];

        /// <summary>Unread total for the badge (independent of paging/filtering).</summary>
        public int UnreadCount { get; init; }
    }

    private static NotificationDto ToDto(Notification n) => new()
    {
        Id = n.Id,
        Kind = n.Kind,
        EnrollmentId = n.EnrollmentId,
        ReferenceNumber = n.ReferenceNumber,
        MessageEn = n.MessageEn,
        MessageAr = n.MessageAr,
        CreatedAtUtc = n.CreatedAtUtc,
        ReadAtUtc = n.ReadAtUtc,
    };

    public sealed class ListHandler(NrsDbContext db)
    {
        public async Task<NotificationListDto> HandleAsync(
            string username, IReadOnlyList<string> roles, bool unreadOnly, int limit, CancellationToken ct)
        {
            var recipients = new List<string>(roles) { username };

            var query = db.Notifications.AsNoTracking()
                .Where(n => recipients.Contains(n.Recipient));

            var unreadCount = await query.CountAsync(n => n.ReadAtUtc == null, ct);

            if (unreadOnly)
            {
                query = query.Where(n => n.ReadAtUtc == null);
            }

            var items = await query
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(Math.Clamp(limit, 1, 100))
                .ToListAsync(ct);

            return new NotificationListDto
            {
                Items = items.Select(ToDto).ToList(),
                UnreadCount = unreadCount,
            };
        }
    }

    public sealed class MarkReadHandler(NrsDbContext db)
    {
        /// <summary>Marks one notification read; scoped to the caller's recipients (no cross-user reads).</summary>
        public async Task<bool> HandleAsync(
            Guid id, string username, IReadOnlyList<string> roles, CancellationToken ct)
        {
            var recipients = new List<string>(roles) { username };
            var notification = await db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && recipients.Contains(n.Recipient), ct);
            if (notification is null)
            {
                return false;
            }

            notification.ReadAtUtc ??= DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }

        /// <summary>Marks everything read for the caller; returns how many were affected.</summary>
        public async Task<int> HandleAllAsync(
            string username, IReadOnlyList<string> roles, CancellationToken ct)
        {
            var recipients = new List<string>(roles) { username };
            var now = DateTimeOffset.UtcNow;
            return await db.Notifications
                .Where(n => recipients.Contains(n.Recipient) && n.ReadAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, now), ct);
        }
    }
}
