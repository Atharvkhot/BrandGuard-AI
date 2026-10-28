using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Domain.Entities;

public class ValidationRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentSubmissionId { get; set; }
    public string Status { get; set; } = "Completed"; // Running, Completed, Failed
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public RiskSeverity OverallRiskLevel { get; set; } = RiskSeverity.Low;
    public int TotalFindings { get; set; } = 0;
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public ContentSubmission? ContentSubmission { get; set; }
    public ICollection<ValidationFinding> Findings { get; set; } = new List<ValidationFinding>();
    public ICollection<ReviewDecision> ReviewDecisions { get; set; } = new List<ReviewDecision>();
}
