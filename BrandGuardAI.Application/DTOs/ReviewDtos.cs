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

public record AcknowledgeFindingsRequest(
    List<Guid> FindingIds,
    string? AcknowledgedBy
);

public record AcknowledgeFindingsResultDto(
    int AcknowledgedCount,
    List<Guid> AcknowledgedFindingIds
);

public record BatchReviewDecisionDto(
    List<Guid> ContentSubmissionIds,
    ReviewDecisionType Decision,
    string ReviewerName,
    string Comments,
    bool ConfirmHighRiskApproval = false
);

public record BatchReviewItemResultDto(
    Guid ContentSubmissionId,
    string Title,
    bool Success,
    string? ErrorMessage,
    ReviewDecisionType? Decision
);

public record BatchReviewResultDto(
    int TotalProcessed,
    int Succeeded,
    int Failed,
    List<BatchReviewItemResultDto> Results
);

