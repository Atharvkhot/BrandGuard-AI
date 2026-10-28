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
