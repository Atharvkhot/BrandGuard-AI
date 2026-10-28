using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Domain.Entities;

public class ValidationFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ValidationRunId { get; set; }
    public Guid? GuidelineChunkId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public RiskSeverity Severity { get; set; } = RiskSeverity.Medium;
    public string OriginalText { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
    public string EvidenceText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ValidationRun? ValidationRun { get; set; }
    public GuidelineChunk? GuidelineChunk { get; set; }
}
