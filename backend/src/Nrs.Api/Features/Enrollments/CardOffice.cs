using Microsoft.EntityFrameworkCore;
using Nrs.Api.Features.Enrollments.Workflow;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Vertical slice: the card office's work — the physical fulfilment of an approved application.
/// A card sits <see cref="CardStatus.IN_PRODUCTION"/> (waiting to be printed) then
/// <see cref="CardStatus.READY_FOR_COLLECTION"/> (waiting to be handed over). Marking a card
/// printed / collected completes the corresponding Camunda user task, which advances the
/// fulfilment flow (dispatch → activate); with no reachable engine the status change is applied
/// directly. Queues are driven by the card status, so they are accurate and lag-free.
/// </summary>
public static class CardOffice
{
    /// <summary>One card in production or awaiting collection, with its applicant details.</summary>
    public record CardTaskDto
    {
        public long IdCardId { get; init; }
        public string CardNumber { get; init; } = null!;
        public string CivilNumber { get; init; } = null!;
        public CardType CardType { get; init; }
        public CardStatus Status { get; init; }
        public Guid EnrollmentId { get; init; }
        public string ReferenceNumber { get; init; } = null!;
        public string FirstNameEn { get; init; } = null!;
        public string FamilyNameEn { get; init; } = null!;
        public string FirstNameAr { get; init; } = null!;
        public string FamilyNameAr { get; init; } = null!;
        public EnrollmentType EnrollmentType { get; init; }
    }

    public sealed class ListHandler(NrsDbContext db)
    {
        public async Task<IReadOnlyList<CardTaskDto>> HandleAsync(CancellationToken cancellationToken)
        {
            var cards = await db.IdCards.AsNoTracking()
                .Where(c => c.EnrollmentId != null
                            && (c.Status == CardStatus.IN_PRODUCTION
                                || c.Status == CardStatus.READY_FOR_COLLECTION))
                .OrderBy(c => c.IdCardId) // FIFO within each queue
                .Take(200)
                .ToListAsync(cancellationToken);
            if (cards.Count == 0)
            {
                return [];
            }

            var enrollmentIds = cards.Select(c => c.EnrollmentId!.Value).ToList();
            var enrollments = await db.Enrollments.AsNoTracking()
                .Where(e => enrollmentIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, cancellationToken);

            return cards
                .Where(c => enrollments.ContainsKey(c.EnrollmentId!.Value))
                .Select(c =>
                {
                    var e = enrollments[c.EnrollmentId!.Value];
                    return new CardTaskDto
                    {
                        IdCardId = c.IdCardId,
                        CardNumber = c.CardNumber,
                        CivilNumber = c.CivilNumber,
                        CardType = c.CardType,
                        Status = c.Status,
                        EnrollmentId = e.Id,
                        ReferenceNumber = e.ReferenceNumber,
                        FirstNameEn = e.FirstNameEn,
                        FamilyNameEn = e.FamilyNameEn,
                        FirstNameAr = e.FirstNameAr,
                        FamilyNameAr = e.FamilyNameAr,
                        EnrollmentType = e.Type,
                    };
                })
                .ToList();
        }
    }

    /// <summary>Outcome of a card-office action, mapped to an HTTP status by the endpoint.</summary>
    public enum Outcome
    {
        /// <summary>The card advanced to its next status (200).</summary>
        Advanced,

        /// <summary>Accepted, but the workflow has not applied the next status yet (202).</summary>
        Accepted,

        /// <summary>No card exists with that id, or it has no originating enrollment (404).</summary>
        NotFound,

        /// <summary>The card is not in the status this action expects (409).</summary>
        WrongStatus,
    }

    public sealed class MarkPrintedHandler(NrsDbContext db, CardFulfilment fulfilment)
    {
        public Task<Outcome> HandleAsync(long cardId, CancellationToken cancellationToken) =>
            AdvanceAsync(
                db, fulfilment, cardId,
                expected: CardStatus.IN_PRODUCTION,
                next: CardStatus.READY_FOR_COLLECTION,
                elementId: CardFulfilment.PrintTaskElementId,
                degrade: async (d, enrollment, ct) =>
                {
                    if (await EnrollmentProvisioning.SetCardStatusAsync(
                            d, enrollment, CardStatus.IN_PRODUCTION, CardStatus.READY_FOR_COLLECTION, ct))
                    {
                        d.Notifications.Add(DecisionNotifications.CardReadyForCollection(enrollment, DateTimeOffset.UtcNow));
                        await d.SaveChangesAsync(ct);
                    }
                },
                cancellationToken);
    }

    public sealed class MarkCollectedHandler(NrsDbContext db, CardFulfilment fulfilment)
    {
        public Task<Outcome> HandleAsync(long cardId, CancellationToken cancellationToken) =>
            AdvanceAsync(
                db, fulfilment, cardId,
                expected: CardStatus.READY_FOR_COLLECTION,
                next: CardStatus.ACTIVE,
                elementId: CardFulfilment.CollectTaskElementId,
                degrade: async (d, enrollment, ct) =>
                {
                    if (await EnrollmentProvisioning.ActivateCardAsync(d, enrollment, ct))
                    {
                        d.Notifications.Add(DecisionNotifications.CardIssued(enrollment, DateTimeOffset.UtcNow));
                        await d.SaveChangesAsync(ct);
                    }
                },
                cancellationToken);
    }

    private static async Task<Outcome> AdvanceAsync(
        NrsDbContext db,
        CardFulfilment fulfilment,
        long cardId,
        CardStatus expected,
        CardStatus next,
        string elementId,
        Func<NrsDbContext, Nrs.Domain.Entities.Enrollment, CancellationToken, Task> degrade,
        CancellationToken cancellationToken)
    {
        var card = await db.IdCards.AsNoTracking().FirstOrDefaultAsync(c => c.IdCardId == cardId, cancellationToken);
        if (card is null || card.EnrollmentId is null)
        {
            return Outcome.NotFound;
        }

        if (card.Status != expected)
        {
            return Outcome.WrongStatus;
        }

        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == card.EnrollmentId, cancellationToken);
        if (enrollment is null)
        {
            return Outcome.NotFound;
        }

        // Complete the Camunda user task so the engine runs the follow-on service task (dispatch /
        // activate), which sets the next status. If that is not possible, apply it directly.
        var completed = await fulfilment.CompleteTaskAsync(enrollment, elementId, cancellationToken);
        if (!completed)
        {
            await degrade(db, enrollment, cancellationToken);
            return Outcome.Advanced;
        }

        // The worker applies the next status sub-second; poll a fresh read so the response reflects it.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        do
        {
            var status = await db.IdCards.AsNoTracking()
                .Where(c => c.IdCardId == cardId)
                .Select(c => c.Status)
                .FirstAsync(cancellationToken);
            if (status == next)
            {
                return Outcome.Advanced;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return Outcome.Accepted;
    }
}
