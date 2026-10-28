using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Application.DTOs;

public record CreateContentSubmissionDto(
    string Title,
    string ContentType,
    string ContentText
);

public record UpdateContentSubmissionDto(
    string Title,
    string ContentType,
    string ContentText
);

public record ContentSubmissionDto(
    Guid Id,
    string Title,
    string ContentType,
    string ContentText,
    ContentSubmissionStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? LatestValidationRunId,
    RiskSeverity? LatestRiskLevel,
    int TotalFindings
);

public record ContentSubmissionDetailDto(
    Guid Id,
    string Title,
    string ContentType,
    string ContentText,
    ContentSubmissionStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? LatestValidationRunId,
    RiskSeverity? LatestRiskLevel,
    int TotalFindings,
    List<ReviewDecisionSummaryDto> Decisions
);

public record ReviewDecisionSummaryDto(
    Guid Id,
    string ReviewerName,
    ReviewDecisionType Decision,
    string Comments,
    DateTime ReviewedAt
);
