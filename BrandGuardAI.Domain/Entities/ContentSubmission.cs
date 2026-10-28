using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Domain.Entities;

public class ContentSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string ContentText { get; set; } = string.Empty;
    public string ContentType { get; set; } = "Social Media Post"; // Social Media Post, Advertisement, Email Campaign, Website Content, Product Description
    public ContentSubmissionStatus Status { get; set; } = ContentSubmissionStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ValidationRun> ValidationRuns { get; set; } = new List<ValidationRun>();
    public ICollection<ReviewDecision> ReviewDecisions { get; set; } = new List<ReviewDecision>();
}
