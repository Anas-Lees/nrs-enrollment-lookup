using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Reports;

/// <summary>
/// Vertical slice: enrollment analytics — the "how is the office doing?" view. Computes the
/// operational KPIs a supervisor (or Camunda Optimize, in a bigger deployment) would want:
/// throughput, straight-through-processing rate, decision outcomes, time-to-decision, SLA
/// escalations, why applications get flagged for review, and per-reviewer workload — all from
/// the enrollment data the review workflow already records.
/// </summary>
public static class ReportsFeature
{
    /// <summary>Marker used in <c>DecidedBy</c> for straight-through (no-human) approvals.</summary>
    private const string AutoDecider = "auto-screening";

    public record FlagCount(string Flag, int Count);

    public record NameCount(string Name, int Count);

    public record DailyVolume(string Date, int Created, int Decided);

    public record EnrollmentReportDto
    {
        /// <summary>Window the report covers, in days.</summary>
        public int WindowDays { get; init; }

        public int Total { get; init; }

        /// <summary>Live pipeline: counts by status (SUBMITTED / UNDER_REVIEW / APPROVED / REJECTED / DRAFT).</summary>
        public IReadOnlyDictionary<string, int> ByStatus { get; init; } = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> ByType { get; init; } = new Dictionary<string, int>();

        public int Decided { get; init; }

        /// <summary>Decided by automated screening (straight-through processing).</summary>
        public int AutoApproved { get; init; }

        public int HumanDecided { get; init; }

        public int Approved { get; init; }

        public int Rejected { get; init; }

        /// <summary>Of all decided applications, the share settled without a human (0–100).</summary>
        public double AutoApprovalRatePct { get; init; }

        /// <summary>Of all decided applications, the share approved (0–100).</summary>
        public double ApprovalRatePct { get; init; }

        /// <summary>Mean hours from submission to decision (null when nothing is decided yet).</summary>
        public double? AvgHoursToDecision { get; init; }

        public int Escalated { get; init; }

        /// <summary>Of applications that reached a human reviewer, the share that breached SLA (0–100).</summary>
        public double EscalationRatePct { get; init; }

        /// <summary>Why applications were routed to a human, most common first.</summary>
        public IReadOnlyList<FlagCount> TopFlags { get; init; } = [];

        /// <summary>Human decisions per reviewer (excludes auto-screening), busiest first.</summary>
        public IReadOnlyList<NameCount> ByReviewer { get; init; } = [];

        /// <summary>Applications created vs decided per day, oldest first.</summary>
        public IReadOnlyList<DailyVolume> Daily { get; init; } = [];
    }

    public sealed class Handler(NrsDbContext db)
    {
        public async Task<EnrollmentReportDto> HandleAsync(int windowDays, CancellationToken cancellationToken)
        {
            var days = Math.Clamp(windowDays, 1, 365);
            var since = DateTimeOffset.UtcNow.AddDays(-days);

            // Project only what the report needs, for enrollments in the window. The enrollment
            // table holds applications (not the population registry), so this stays small; a
            // production report over huge volumes would push more of this into SQL.
            var rows = await db.Enrollments.AsNoTracking()
                .Where(e => e.CreatedAtUtc >= since)
                .Select(e => new Row(
                    e.Status, e.Type, e.CreatedAtUtc, e.DecidedAtUtc, e.DecidedBy, e.EscalatedAtUtc, e.ScreeningFlags))
                .ToListAsync(cancellationToken);

            var decidedRows = rows.Where(r => r.Status is EnrollmentStatus.APPROVED or EnrollmentStatus.REJECTED).ToList();
            var decided = decidedRows.Count;
            var autoApproved = decidedRows.Count(r => r.DecidedBy == AutoDecider);
            var approved = decidedRows.Count(r => r.Status == EnrollmentStatus.APPROVED);
            var humanDecided = decided - autoApproved;

            // Applications that reached a human: still under review, or human-decided.
            var underReview = rows.Count(r => r.Status == EnrollmentStatus.UNDER_REVIEW);
            var reachedHuman = underReview + humanDecided;
            var escalated = rows.Count(r => r.EscalatedAtUtc is not null);

            var avgHours = decidedRows
                .Where(r => r.DecidedAtUtc is not null)
                .Select(r => (r.DecidedAtUtc!.Value - r.CreatedAtUtc).TotalHours)
                .DefaultIfEmpty()
                .Average();

            var flags = rows
                .Where(r => !string.IsNullOrEmpty(r.ScreeningFlags))
                .SelectMany(r => r.ScreeningFlags!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .GroupBy(f => f)
                .Select(g => new FlagCount(g.Key, g.Count()))
                .OrderByDescending(f => f.Count)
                .ToList();

            var byReviewer = decidedRows
                .Where(r => r.DecidedBy is not null && r.DecidedBy != AutoDecider)
                .GroupBy(r => r.DecidedBy!)
                .Select(g => new NameCount(g.Key, g.Count()))
                .OrderByDescending(r => r.Count)
                .ToList();

            // Daily created / decided for the last 14 days of the window (a readable trend).
            var trendDays = Math.Min(days, 14);
            var today = DateTimeOffset.UtcNow.UtcDateTime.Date;
            var daily = Enumerable.Range(0, trendDays)
                .Select(offset => today.AddDays(-(trendDays - 1 - offset)))
                .Select(date => new DailyVolume(
                    date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    rows.Count(r => r.CreatedAtUtc.UtcDateTime.Date == date),
                    decidedRows.Count(r => r.DecidedAtUtc is not null && r.DecidedAtUtc.Value.UtcDateTime.Date == date)))
                .ToList();

            return new EnrollmentReportDto
            {
                WindowDays = days,
                Total = rows.Count,
                ByStatus = rows.GroupBy(r => r.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                ByType = rows.GroupBy(r => r.Type.ToString()).ToDictionary(g => g.Key, g => g.Count()),
                Decided = decided,
                AutoApproved = autoApproved,
                HumanDecided = humanDecided,
                Approved = approved,
                Rejected = decided - approved,
                AutoApprovalRatePct = Pct(autoApproved, decided),
                ApprovalRatePct = Pct(approved, decided),
                AvgHoursToDecision = decided == 0 ? null : Math.Round(avgHours, 1),
                Escalated = escalated,
                EscalationRatePct = Pct(escalated, reachedHuman),
                TopFlags = flags,
                ByReviewer = byReviewer,
                Daily = daily,
            };
        }

        private static double Pct(int part, int whole) =>
            whole == 0 ? 0 : Math.Round(part * 100.0 / whole, 1);
    }

    private sealed record Row(
        EnrollmentStatus Status,
        EnrollmentType Type,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? DecidedAtUtc,
        string? DecidedBy,
        DateTimeOffset? EscalatedAtUtc,
        string? ScreeningFlags);
}
