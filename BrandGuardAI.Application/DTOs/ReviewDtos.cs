using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Application.DTOs;

public record SubmitReviewDecisionDto(
    Guid ContentSubmissionId,
    Guid? ValidationRunId,
    string ReviewerName,
    ReviewDecisionType Decision,
    string Comments
);

public record ReviewDecisionDto(
    Guid Id,
    Guid ContentSubmissionId,
    Guid? ValidationRunId,
    string ContentTitle,
    string ContentType,
    string ReviewerName,
    ReviewDecisionType Decision,
    string Comments,
    DateTime ReviewedAt
);

public record ReviewHistoryItemDto(
    Guid Id,
    Guid ContentSubmissionId,
    Guid? ValidationRunId,
    string ContentTitle,
    string ContentType,
    string ReviewerName,
    ReviewDecisionType Decision,
    string Comments,
    DateTime ReviewedAt,
    RiskSeverity? OverallRiskLevel,
    int TotalFindings
);
