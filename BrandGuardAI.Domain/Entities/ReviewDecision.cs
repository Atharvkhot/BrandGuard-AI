using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Domain.Entities;

public class ReviewDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContentSubmissionId { get; set; }
    public Guid? ValidationRunId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public ReviewDecisionType Decision { get; set; } = ReviewDecisionType.Approved;
    public string Comments { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ContentSubmission? ContentSubmission { get; set; }
    public ValidationRun? ValidationRun { get; set; }
}
