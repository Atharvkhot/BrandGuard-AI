using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Application.DTOs;

public record DashboardStatsDto(
    int TotalGuidelines,
    int TotalContentSubmissions,
    int PendingReviews,
    int ApprovedSubmissions,
    int RejectedSubmissions,
    int ChangesRequestedSubmissions,
    int HighRiskFindings,
    int MediumRiskFindings,
    int LowRiskFindings,
    List<RecentSubmissionItemDto> RecentSubmissions
);

public record RecentSubmissionItemDto(
    Guid Id,
    string Title,
    string ContentType,
    DateTime CreatedAt,
    RiskSeverity? RiskLevel,
    ContentSubmissionStatus Status,
    int TotalFindings,
    Guid? LatestValidationRunId
);

public record GovernanceOverviewDto(
    int TotalSubmissions,
    int TotalValidationRuns,
    int TotalFindings,
    int HighRiskFindings,
    int MediumRiskFindings,
    int LowRiskFindings,
    int PendingReviews,
    int ApprovedSubmissions,
    int RejectedSubmissions,
    int ChangesRequestedSubmissions,
    int TotalGuidelines
);

public record ApprovalAnalyticsDto(
    int TotalReviewedSubmissions,
    int ApprovedCount,
    int RejectedCount,
    int ChangesRequestedCount,
    double ApprovalRate,
    double RejectionRate,
    double ChangesRequestedRate,
    int TotalReviewEvents
);

public record DailyViolationTrendDto(
    string Date,
    int TotalFindings,
    int HighRiskCount,
    int MediumRiskCount,
    int LowRiskCount
);

public record ViolatedGuidelineDto(
    string GuidelineOrRuleName,
    int ViolationCount,
    double PercentageOfTotal,
    int HighRiskCount,
    int MediumRiskCount,
    int LowRiskCount,
    bool IsVerifiedGuideline,
    bool IsDeterministicRule
);

public record GovernanceHealthScoreDto(
    double? Score,
    string StatusLabel,
    string FormulaExplanation,
    double BaseScore,
    double FindingPenalty,
    double DecisionAdjustment,
    double AcknowledgmentBonus,
    bool IsIndicatorOnly = true,
    bool HasSufficientData = true
);

public record GovernanceActivityDto(
    Guid Id,
    string ActivityType, // "Validation" or "HumanReview"
    DateTime Timestamp,
    string Title,
    string Details,
    string? Severity,
    string? ReviewerOrActor
);

public record GovernanceIntelligenceDto(
    GovernanceOverviewDto Overview,
    ApprovalAnalyticsDto ApprovalAnalytics,
    List<DailyViolationTrendDto> ViolationTrends,
    List<ViolatedGuidelineDto> TopViolatedGuidelines,
    GovernanceHealthScoreDto HealthScore,
    List<GovernanceActivityDto> RecentActivities,
    int SelectedDays,
    DateTime GeneratedAt
);
