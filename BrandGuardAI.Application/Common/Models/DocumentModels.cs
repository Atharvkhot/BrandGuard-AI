namespace BrandGuardAI.Application.Common.Models;

public record PdfPageContent(int PageNumber, string Text);

public record DocumentChunk(
    int ChunkIndex,
    string Text,
    int PageNumber,
    string SectionTitle
);

public record ChunkSearchResult(
    Guid ChunkId,
    Guid GuidelineId,
    string GuidelineTitle,
    int PageNumber,
    string SectionTitle,
    string ChunkText,
    float SimilarityScore
);
