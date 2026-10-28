using BrandGuardAI.Domain.Enums;

namespace BrandGuardAI.Domain.Entities;

public class BrandGuideline
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public GuidelineStatus Status { get; set; } = GuidelineStatus.Uploading;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<GuidelineChunk> Chunks { get; set; } = new List<GuidelineChunk>();
    public ICollection<ValidationRule> ValidationRules { get; set; } = new List<ValidationRule>();
}
