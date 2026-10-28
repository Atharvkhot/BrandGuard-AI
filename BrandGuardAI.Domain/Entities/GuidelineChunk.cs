namespace BrandGuardAI.Domain.Entities;

public class GuidelineChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandGuidelineId { get; set; }
    public int ChunkIndex { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int PageNumber { get; set; } = 1;
    public string SectionTitle { get; set; } = string.Empty;
    public string EmbeddingData { get; set; } = string.Empty; // Stored as JSON float[] vector
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public BrandGuideline? BrandGuideline { get; set; }
    public ICollection<ValidationFinding> Findings { get; set; } = new List<ValidationFinding>();
}
