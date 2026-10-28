using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Application.Common.Models;

public record RuleEvaluationResult(
    Guid RuleId,
    string RuleName,
    RuleType RuleType,
    RiskSeverity Severity,
    string MatchedText,
    string Explanation,
    string SuggestedFix,
    string EvidenceText,
    Guid? SourceGuidelineId
);

public record LlmValidationRequest(
    string ContentTitle,
    string ContentType,
    string ContentText,
    List<ChunkSearchResult> RetrievedChunks,
    List<RuleEvaluationResult> DeterministicFindings
);

public record LlmFindingDto(
    string IssueCategory,
    RiskSeverity Severity,
    string AffectedText,
    string Explanation,
    string SuggestedCorrection,
    Guid? CitedChunkId,
    int? CitedPageNumber,
    string? EvidenceQuote
);

public record LlmValidationResponse(
    List<LlmFindingDto> Findings,
    string OverallAssessment,
    string? SuggestedToneAdjustment
);

public record FindingDto(
    Guid Id,
    Guid ValidationRunId,
    Guid? GuidelineChunkId,
    string IssueType,
    RiskSeverity Severity,
    string OriginalText,
    string Explanation,
    string SuggestedFix,
    string EvidenceText,
    string? SourceGuidelineTitle,
    int? PageNumber,
    string? SectionTitle,
    DateTime CreatedAt
);

public record ValidationResultDto(
    Guid ValidationRunId,
    Guid ContentSubmissionId,
    string SubmissionTitle,
    string ContentType,
    string OriginalContent,
    string Status,
    RiskSeverity OverallRiskLevel,
    int TotalFindings,
    int HighRiskCount,
    int MediumRiskCount,
    int LowRiskCount,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    List<FindingDto> Findings
);
