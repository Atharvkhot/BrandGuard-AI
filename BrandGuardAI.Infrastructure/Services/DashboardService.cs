using BrandGuardAI.Application.Common.Interfaces;
using BrandGuardAI.Application.DTOs;
using BrandGuardAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BrandGuardAI.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;

    public DashboardService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(int? days = null, CancellationToken cancellationToken = default)
    {
        DateTime? cutoffUtc = days.HasValue && days.Value > 0
            ? DateTime.UtcNow.Date.AddDays(-days.Value + 1)
            : null;

        var totalGuidelines = await _context.BrandGuidelines.AsNoTracking().CountAsync(cancellationToken);

        var submissionsQuery = _context.ContentSubmissions.AsNoTracking();
        if (cutoffUtc.HasValue)
        {
            submissionsQuery = submissionsQuery.Where(s => s.CreatedAt >= cutoffUtc.Value);
        }

        var totalSubmissions = await submissionsQuery.CountAsync(cancellationToken);

        var pendingReviews = await submissionsQuery
            .CountAsync(s => s.Status == ContentSubmissionStatus.PendingReview || s.Status == ContentSubmissionStatus.Validating, cancellationToken);

        var approvedSubmissions = await submissionsQuery
            .CountAsync(s => s.Status == ContentSubmissionStatus.Approved, cancellationToken);

        var rejectedSubmissions = await submissionsQuery
            .CountAsync(s => s.Status == ContentSubmissionStatus.Rejected, cancellationToken);

        var changesRequested = await submissionsQuery
            .CountAsync(s => s.Status == ContentSubmissionStatus.ChangesRequested, cancellationToken);

        var findingsQuery = _context.ValidationFindings.AsNoTracking();
        if (cutoffUtc.HasValue)
        {
            findingsQuery = findingsQuery.Where(f => f.CreatedAt >= cutoffUtc.Value);
        }

        var highRiskFindings = await findingsQuery
            .CountAsync(f => f.Severity == RiskSeverity.High, cancellationToken);

        var mediumRiskFindings = await findingsQuery
            .CountAsync(f => f.Severity == RiskSeverity.Medium, cancellationToken);

        var lowRiskFindings = await findingsQuery
            .CountAsync(f => f.Severity == RiskSeverity.Low, cancellationToken);

        var recentSubmissions = await _context.ContentSubmissions
            .AsNoTracking()
            .Include(s => s.ValidationRuns)
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .Select(s => new RecentSubmissionItemDto(
                s.Id,
                s.Title,
                s.ContentType,
                s.CreatedAt,
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => (RiskSeverity?)v.OverallRiskLevel).FirstOrDefault(),
                s.Status,
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => v.TotalFindings).FirstOrDefault(),
                s.ValidationRuns.OrderByDescending(v => v.StartedAt).Select(v => (Guid?)v.Id).FirstOrDefault()
            ))
            .ToListAsync(cancellationToken);

        return new DashboardStatsDto(
            totalGuidelines,
            totalSubmissions,
            pendingReviews,
            approvedSubmissions,
            rejectedSubmissions,
            changesRequested,
            highRiskFindings,
            mediumRiskFindings,
            lowRiskFindings,
            recentSubmissions
        );
    }

    public async Task<GovernanceIntelligenceDto> GetGovernanceIntelligenceAsync(int? days = 30, CancellationToken cancellationToken = default)
    {
        int effectiveDays = (days.HasValue && days.Value > 0) ? days.Value : 30;
        DateTime todayUtc = DateTime.UtcNow.Date;
        DateTime cutoffUtc = todayUtc.AddDays(-effectiveDays + 1);

        // --- 1. OVERVIEW STATISTICS ---
        var totalGuidelines = await _context.BrandGuidelines.AsNoTracking().CountAsync(cancellationToken);

        var submissionsInRange = await _context.ContentSubmissions
            .AsNoTracking()
            .Where(s => s.CreatedAt >= cutoffUtc)
            .Select(s => new { s.Id, s.Status, s.CreatedAt })
            .ToListAsync(cancellationToken);

        int totalSubmissions = submissionsInRange.Count;
        int pendingReviews = submissionsInRange.Count(s => s.Status == ContentSubmissionStatus.PendingReview || s.Status == ContentSubmissionStatus.Validating);
        int approvedSubmissions = submissionsInRange.Count(s => s.Status == ContentSubmissionStatus.Approved);
        int rejectedSubmissions = submissionsInRange.Count(s => s.Status == ContentSubmissionStatus.Rejected);
        int changesRequestedSubmissions = submissionsInRange.Count(s => s.Status == ContentSubmissionStatus.ChangesRequested);

        int totalValidationRuns = await _context.ValidationRuns
            .AsNoTracking()
            .Where(v => v.StartedAt >= cutoffUtc)
            .CountAsync(cancellationToken);

        // Query findings in date range
        var findingsInRange = await _context.ValidationFindings
            .AsNoTracking()
            .Include(f => f.GuidelineChunk)
                .ThenInclude(c => c!.BrandGuideline)
            .Where(f => f.CreatedAt >= cutoffUtc)
            .ToListAsync(cancellationToken);

        int totalFindings = findingsInRange.Count;
        int highRiskFindings = findingsInRange.Count(f => f.Severity == RiskSeverity.High);
        int mediumRiskFindings = findingsInRange.Count(f => f.Severity == RiskSeverity.Medium);
        int lowRiskFindings = findingsInRange.Count(f => f.Severity == RiskSeverity.Low);
        int acknowledgedFindingsCount = findingsInRange.Count(f => f.IsAcknowledged);

        var overview = new GovernanceOverviewDto(
            totalSubmissions,
            totalValidationRuns,
            totalFindings,
            highRiskFindings,
            mediumRiskFindings,
            lowRiskFindings,
            pendingReviews,
            approvedSubmissions,
            rejectedSubmissions,
            changesRequestedSubmissions,
            totalGuidelines
        );

        // --- 2. APPROVAL ANALYTICS (ACCURATE DENOMINATORS) ---
        // Query distinct submissions reviewed within the date range
        var reviewedDecisionsInRange = await _context.ReviewDecisions
            .AsNoTracking()
            .Where(d => d.ReviewedAt >= cutoffUtc)
            .ToListAsync(cancellationToken);

        int totalReviewEvents = reviewedDecisionsInRange.Count;

        // Group by submission to get latest decision per submission in range
        var latestDecisionPerSub = reviewedDecisionsInRange
            .GroupBy(d => d.ContentSubmissionId)
            .Select(g => g.OrderByDescending(d => d.ReviewedAt).First())
            .ToList();

        // Also check submissions whose status is finalized in this date range
        int reviewedSubsCount = latestDecisionPerSub.Count;
        int approvedCount = latestDecisionPerSub.Count(d => d.Decision == ReviewDecisionType.Approved);
        int rejectedCount = latestDecisionPerSub.Count(d => d.Decision == ReviewDecisionType.Rejected);
        int changesRequestedCount = latestDecisionPerSub.Count(d => d.Decision == ReviewDecisionType.ChangesRequested);

        // If no review records were found within cutoff, fallback to finalized submissions in range
        if (reviewedSubsCount == 0)
        {
            var finalizedSubs = submissionsInRange
                .Where(s => s.Status == ContentSubmissionStatus.Approved ||
                            s.Status == ContentSubmissionStatus.Rejected ||
                            s.Status == ContentSubmissionStatus.ChangesRequested)
                .ToList();

            reviewedSubsCount = finalizedSubs.Count;
            approvedCount = finalizedSubs.Count(s => s.Status == ContentSubmissionStatus.Approved);
            rejectedCount = finalizedSubs.Count(s => s.Status == ContentSubmissionStatus.Rejected);
            changesRequestedCount = finalizedSubs.Count(s => s.Status == ContentSubmissionStatus.ChangesRequested);
        }

        double approvalRate = reviewedSubsCount > 0 ? Math.Round((double)approvedCount / reviewedSubsCount * 100.0, 1) : 0.0;
        double rejectionRate = reviewedSubsCount > 0 ? Math.Round((double)rejectedCount / reviewedSubsCount * 100.0, 1) : 0.0;
        double changesRequestedRate = reviewedSubsCount > 0 ? Math.Round((double)changesRequestedCount / reviewedSubsCount * 100.0, 1) : 0.0;

        var approvalAnalytics = new ApprovalAnalyticsDto(
            reviewedSubsCount,
            approvedCount,
            rejectedCount,
            changesRequestedCount,
            approvalRate,
            rejectionRate,
            changesRequestedRate,
            totalReviewEvents
        );

        // --- 3. VIOLATION TRENDS (ALL DATES IN RANGE INCLUDED) ---
        var trends = new List<DailyViolationTrendDto>();
        for (int i = 0; i < effectiveDays; i++)
        {
            DateTime currentDay = cutoffUtc.AddDays(i);
            string dateStr = currentDay.ToString("yyyy-MM-dd");

            var dayFindings = findingsInRange
                .Where(f => f.CreatedAt.Date == currentDay.Date)
                .ToList();

            int dayTotal = dayFindings.Count;
            int dayHigh = dayFindings.Count(f => f.Severity == RiskSeverity.High);
            int dayMed = dayFindings.Count(f => f.Severity == RiskSeverity.Medium);
            int dayLow = dayFindings.Count(f => f.Severity == RiskSeverity.Low);

            trends.Add(new DailyViolationTrendDto(dateStr, dayTotal, dayHigh, dayMed, dayLow));
        }

        // --- 4. TOP 5 VIOLATED GUIDELINES OR DETERMINISTIC RULES ---
        var activeRuleNames = await _context.ValidationRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Select(r => r.RuleName)
            .ToListAsync(cancellationToken);

        var topGuidelines = findingsInRange
            .GroupBy(f =>
            {
                bool hasVerifiedChunk = f.GuidelineChunkId.HasValue &&
                                        f.GuidelineChunk?.BrandGuideline != null &&
                                        !string.IsNullOrWhiteSpace(f.GuidelineChunk.BrandGuideline.Title);

                bool isDeterministic = activeRuleNames.Contains(f.IssueType, StringComparer.OrdinalIgnoreCase);

                if (hasVerifiedChunk)
                {
                    var gTitle = f.GuidelineChunk!.BrandGuideline!.Title;
                    var issue = string.IsNullOrWhiteSpace(f.IssueType) ? "Policy Citation" : f.IssueType;
                    return (Name: $"{gTitle}: {issue}", IsVerified: true, IsDeterministic: false);
                }
                else if (isDeterministic)
                {
                    return (Name: $"{f.IssueType} (Deterministic Rule)", IsVerified: false, IsDeterministic: true);
                }
                else
                {
                    var issue = string.IsNullOrWhiteSpace(f.IssueType) ? "Unclassified Heuristic" : f.IssueType;
                    return (Name: $"{issue} (Heuristic AI Finding — Unverified)", IsVerified: false, IsDeterministic: false);
                }
            })
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new ViolatedGuidelineDto(
                g.Key.Name,
                g.Count(),
                totalFindings > 0 ? Math.Round((double)g.Count() / totalFindings * 100.0, 1) : 0.0,
                g.Count(f => f.Severity == RiskSeverity.High),
                g.Count(f => f.Severity == RiskSeverity.Medium),
                g.Count(f => f.Severity == RiskSeverity.Low),
                g.Key.IsVerified,
                g.Key.IsDeterministic
            ))
            .ToList();

        // --- 5. GOVERNANCE HEALTH SCORE (TRANSPARENT & EXPLAINABLE) ---
        double baseScore = 100.0;
        double findingPenalty = 0.0;
        double decisionAdjustment = 0.0;
        double ackBonus = 0.0;
        double? finalScore = null;
        string statusLabel;
        bool hasSufficientData;

        if (totalSubmissions == 0)
        {
            finalScore = null;
            statusLabel = "Not enough data";
            hasSufficientData = false;
        }
        else if (totalFindings == 0)
        {
            hasSufficientData = true;
            statusLabel = "Optimal (Zero Violations Detected)";
            if (reviewedSubsCount > 0)
            {
                double appRatio = (double)approvedCount / reviewedSubsCount;
                double rejRatio = (double)rejectedCount / reviewedSubsCount;
                decisionAdjustment = Math.Round((appRatio * 10.0) - (rejRatio * 15.0), 1);
                finalScore = Math.Round(Math.Clamp(baseScore + decisionAdjustment, 0.0, 100.0), 1);
            }
            else
            {
                finalScore = 100.0;
            }
        }
        else
        {
            hasSufficientData = true;
            // Finding penalty weighted by severity per submission:
            // High = 10 pts, Medium = 4 pts, Low = 1 pt
            double weightedFindingSum = (highRiskFindings * 10.0) + (mediumRiskFindings * 4.0) + (lowRiskFindings * 1.0);
            findingPenalty = Math.Round(weightedFindingSum / Math.Max(totalSubmissions, 1), 1);

            // Review decision adjustment (bonus for high approvals, penalty for high rejections)
            if (reviewedSubsCount > 0)
            {
                double appRatio = (double)approvedCount / reviewedSubsCount;
                double rejRatio = (double)rejectedCount / reviewedSubsCount;
                decisionAdjustment = Math.Round((appRatio * 10.0) - (rejRatio * 15.0), 1);
            }

            // Human acknowledgment bonus (rewarding active human oversight)
            if (totalFindings > 0)
            {
                double ackRatio = (double)acknowledgedFindingsCount / totalFindings;
                ackBonus = Math.Round(ackRatio * 5.0, 1);
            }

            double rawScore = baseScore - findingPenalty + decisionAdjustment + ackBonus;
            finalScore = Math.Round(Math.Clamp(rawScore, 0.0, 100.0), 1);

            statusLabel = finalScore.Value switch
            {
                >= 85.0 => "Optimal Governance (Low Compliance Risk)",
                >= 70.0 => "Good Compliance (Moderate Operational Risk)",
                >= 50.0 => "Action Recommended (Elevated Policy Violations)",
                _ => "Critical Compliance Attention Required"
            };
        }

        string formulaExplanation = totalSubmissions == 0
            ? "Not enough data to calculate Governance Health Score. Submit marketing content to activate automated compliance telemetry."
            : "BrandGuard AI Governance Health Score = Base (100) - Weighted Finding Penalty per Submission [High: 10, Med: 4, Low: 1] + Review Adjustment [(ApprovalRatio * 10) - (RejectionRatio * 15)] + Human Acknowledgment Oversight Bonus (up to +5). Clamped between 0 and 100. Internal BrandGuard AI indicator only, not an external industry benchmark.";

        var healthScore = new GovernanceHealthScoreDto(
            finalScore,
            statusLabel,
            formulaExplanation,
            baseScore,
            findingPenalty,
            decisionAdjustment,
            ackBonus,
            IsIndicatorOnly: true,
            HasSufficientData: hasSufficientData
        );

        // --- 6. RECENT GOVERNANCE ACTIVITY (VALIDATIONS + REVIEWS) ---
        var recentValidations = await _context.ValidationRuns
            .AsNoTracking()
            .Include(v => v.ContentSubmission)
            .OrderByDescending(v => v.StartedAt)
            .Take(8)
            .Select(v => new GovernanceActivityDto(
                v.Id,
                "Validation",
                v.StartedAt,
                v.ContentSubmission != null ? v.ContentSubmission.Title : "Marketing Content",
                $"Automated AI validation completed. Risk: {v.OverallRiskLevel}, {v.TotalFindings} findings.",
                v.OverallRiskLevel.ToString(),
                "AI Policy Engine"
            ))
            .ToListAsync(cancellationToken);

        var recentReviews = await _context.ReviewDecisions
            .AsNoTracking()
            .Include(d => d.ContentSubmission)
            .OrderByDescending(d => d.ReviewedAt)
            .Take(8)
            .Select(d => new GovernanceActivityDto(
                d.Id,
                "HumanReview",
                d.ReviewedAt,
                d.ContentSubmission != null ? d.ContentSubmission.Title : "Marketing Content",
                $"Human review decision: {d.Decision}. Notes: {(string.IsNullOrWhiteSpace(d.Comments) ? "No comments provided." : d.Comments)}",
                d.Decision.ToString(),
                d.ReviewerName
            ))
            .ToListAsync(cancellationToken);

        var combinedActivities = recentValidations
            .Concat(recentReviews)
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToList();

        return new GovernanceIntelligenceDto(
            overview,
            approvalAnalytics,
            trends,
            topGuidelines,
            healthScore,
            combinedActivities,
            effectiveDays,
            DateTime.UtcNow
        );
    }
}
